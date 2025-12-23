using System.Windows;
using Multiboxer.App.Services;
using Multiboxer.Core.Config;
using Multiboxer.Core.Input;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Performance;
using Multiboxer.Core.Slots;
using Multiboxer.Core.VirtualFiles;
using Multiboxer.Overlay;

namespace Multiboxer.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Core services
    public static SlotManager SlotManager { get; private set; } = null!;
    public static LayoutEngine LayoutEngine { get; private set; } = null!;
    public static HotkeyManager HotkeyManager { get; private set; } = null!;
    public static OverlayManager OverlayManager { get; private set; } = null!;
    public static ThumbnailManager ThumbnailManager { get; private set; } = null!;
    public static ConfigManager ConfigManager { get; private set; } = null!;
    public static AffinityManager AffinityManager { get; private set; } = null!;
    public static TrayIconService TrayIconService { get; private set; } = null!;
    public static VirtualFileManager VirtualFileManager { get; private set; } = null!;

    /// <summary>
    /// Flag to indicate batch launch is in progress - disables per-slot layout updates
    /// </summary>
    public static bool IsBatchLaunching { get; set; } = false;

    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enable debug logging to file for troubleshooting
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "multiboxer_debug.log");
            // Use FileShare.ReadWrite to allow other processes to access the file
            var logStream = new System.IO.FileStream(logPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
            var logWriter = new System.IO.StreamWriter(logStream) { AutoFlush = true };
            System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(logWriter));
            System.Diagnostics.Trace.AutoFlush = true;
            System.Diagnostics.Trace.WriteLine($"========== Multiboxer Started {DateTime.Now} ==========");
        }
        catch (Exception ex)
        {
            // If we can't create the log file, just continue without file logging
            Debug.WriteLine($"Could not create log file: {ex.Message}");
        }

        // Add global exception handlers to prevent silent crashes
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Debug.WriteLine($"Unhandled exception: {ex?.Message}\n{ex?.StackTrace}");
            MessageBox.Show($"Fatal error: {ex?.Message}", "EQBZ Multiboxer Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"Dispatcher exception: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"Error: {args.Exception.Message}", "EQBZ Multiboxer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // Prevent crash
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Debug.WriteLine($"Task exception: {args.Exception.Message}");
            args.SetObserved(); // Prevent crash
        };

        // Initialize core services
        InitializeServices();

        // Register hotkeys
        HotkeyManager.RegisterDefaultSlotHotkeys();
        HotkeyManager.RegisterDefaultNavigationHotkeys();

        // Wire up hotkey events
        HotkeyManager.HotkeyPressed += OnHotkeyPressed;

        // Wire up slot activation events for auto-borderless and layout
        SlotManager.SlotActivated += OnSlotActivated;

        // Initialize tray icon
        TrayIconService.Initialize();

        // Create and show main window
        _mainWindow = new MainWindow();

        // Check if should start minimized
        if (ConfigManager.Settings.Window?.StartMinimized == true)
        {
            _mainWindow.WindowState = WindowState.Minimized;
            // The window will hide itself to tray on minimize
        }

        _mainWindow.Show();
    }

    private void InitializeServices()
    {
        // Create config manager and load settings
        var configPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Multiboxer");
        ConfigManager = new ConfigManager(configPath);
        ConfigManager.Load();

        // Create slot manager
        SlotManager = new SlotManager();

        // Create affinity manager
        AffinityManager = new AffinityManager();

        // Create layout engine
        LayoutEngine = new LayoutEngine(SlotManager);

        // Apply settings to layout engine
        if (ConfigManager.Settings.Layout != null)
        {
            LayoutEngine.Options = ConfigManager.Settings.Layout.Options;
            if (!string.IsNullOrEmpty(ConfigManager.Settings.Layout.ActiveLayout))
            {
                LayoutEngine.SetStrategy(ConfigManager.Settings.Layout.ActiveLayout);
            }

            // Load custom layouts
            foreach (var customLayout in ConfigManager.Settings.Layout.CustomLayouts)
            {
                LayoutEngine.AddCustomLayout(customLayout);
            }
        }

        // Auto-load saved window layout regions into LayoutEngine
        // This allows hotkey swapping to work immediately after slots are assigned
        LoadSavedLayoutRegions();

        // Create hotkey manager (using low-level hook for global hotkeys)
        HotkeyManager = new HotkeyManager();

        // Create overlay manager
        OverlayManager = new OverlayManager(SlotManager);

        // Apply highlighter settings
        if (ConfigManager.Settings.Highlighter != null)
        {
            OverlayManager.ShowBorder = ConfigManager.Settings.Highlighter.ShowBorder;
            OverlayManager.ShowNumber = ConfigManager.Settings.Highlighter.ShowNumber;
        }

        // Create thumbnail manager (Video FX style)
        ThumbnailManager = new ThumbnailManager();
        ThumbnailManager.ThumbnailClicked += OnThumbnailClicked;

        // Clean up thumbnails when slots disappear
        SlotManager.SlotRemoved += OnSlotRemoved;
        SlotManager.SlotProcessExited += OnSlotProcessExited;

        // Create tray icon service
        TrayIconService = new TrayIconService(SlotManager);

        // Create virtual file manager
        var virtualFilesBackupPath = System.IO.Path.Combine(configPath, "virtualfiles_backup");
        VirtualFileManager = new VirtualFileManager(virtualFilesBackupPath);
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        // Handle slot hotkeys
        if (e.SlotId.HasValue)
        {
            var slotId = e.SlotId.Value;
            var slot = SlotManager.GetSlot(slotId);

            // Only process if this slot exists and has a process
            if (slot == null || !slot.HasProcess)
            {
                Debug.WriteLine($"Hotkey for slot {slotId} ignored - slot not active");
                return;
            }

            // ISBoxer-style approach: Focus first, then apply layout
            // Using async to allow proper sequencing without blocking
            _ = FocusAndApplyLayoutAsync(slotId);
        }
        else
        {
            // Handle navigation hotkeys
            switch (e.Action.ToLowerInvariant())
            {
                case "nextwindow":
                    _ = FocusNextAndApplyLayoutAsync();
                    break;
                case "previouswindow":
                    _ = FocusPreviousAndApplyLayoutAsync();
                    break;
            }
        }
    }

    /// <summary>
    /// Handle slot activation - apply borderless and update layout
    /// Like JMB: automatically makes windows borderless and adjusts layout
    /// </summary>
    private void OnSlotActivated(object? sender, SlotEventArgs e)
    {
        // Run on UI thread with a delay to ensure window handle is ready
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            // Wait for the window to be fully created
            await Task.Delay(500);

            var slot = e.Slot;
            if (slot?.MainWindowHandle == IntPtr.Zero)
            {
                // Try to refresh the window handle
                slot?.RefreshWindowHandle();
                await Task.Delay(200);
            }

            if (slot?.MainWindowHandle != IntPtr.Zero)
            {
                // Make window borderless if option is enabled
                if (LayoutEngine.Options.MakeBorderless)
                {
                    Debug.WriteLine($"Slot {slot.Id} activated: Making borderless");
                    Multiboxer.Core.Window.WindowHelper.MakeBorderless(slot.MainWindowHandle);
                }

                // Apply custom window title if enabled
                if (ConfigManager.Settings.RenameWindows)
                {
                    ApplyCustomWindowTitle(slot);
                }

                // Remap regions to slots now that we have a new slot
                LayoutEngine.RemapRegionsToSlots();

                // During batch launch, skip layout application - it will be done at the end
                if (IsBatchLaunching)
                {
                    Debug.WriteLine($"Slot {slot.Id} activated: Batch launch in progress, skipping layout");
                    return;
                }

                // Only auto-apply if enabled
                if (!LayoutEngine.Options.SwapOnActivate)
                {
                    Debug.WriteLine($"Slot {slot.Id} activated: SwapOnActivate disabled, not applying layout");
                    return;
                }

                // Apply layout if we have regions defined and enough slots to fill the template
                if (LayoutEngine.SlotRegions.Count > 0)
                {
                    var activeSlots = SlotManager.GetActiveSlots().ToList();
                    if (!LayoutEngine.HasEnoughSlotsForTemplate(activeSlots.Count))
                    {
                        Debug.WriteLine($"Slot {slot.Id} activated: Waiting for more slots before applying layout (have {activeSlots.Count}, need {LayoutEngine.TemplateRegionCount})");
                        return;
                    }

                    Debug.WriteLine($"Slot {slot.Id} activated: Applying layout with {LayoutEngine.SlotRegions.Count} regions");

                    // Apply layout - first active slot becomes foreground if none set
                    if (activeSlots.Count == 1)
                    {
                        LayoutEngine.ApplyLayoutWithMain(slot.Id);
                    }
                    else
                    {
                        var foregroundId = LayoutEngine.CurrentForegroundSlotId;
                        if (foregroundId == 0 && activeSlots.Count > 0)
                            foregroundId = activeSlots[0].Id;

                        LayoutEngine.ApplyLayoutWithMain(foregroundId);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Focus a specific slot and apply layout - ISBoxer-style sequencing
    /// Auto-loads layout and applies borderless if needed
    /// </summary>
    private async Task FocusAndApplyLayoutAsync(int slotId)
    {
        try
        {
            // Focus the slot's window first
            var focused = SlotManager.FocusSlot(slotId);

            if (!focused)
                return;

            // Run on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Auto-load layout if not already loaded
                if (LayoutEngine.SlotRegions.Count == 0)
                {
                    var savedLayouts = ConfigManager.Settings.SavedWindowLayouts;
                    var activeLayoutName = ConfigManager.Settings.ActiveSavedLayoutName;

                    if (savedLayouts != null && savedLayouts.Count > 0)
                    {
                        var layout = savedLayouts.FirstOrDefault(l =>
                            l.Name.Equals(activeLayoutName, StringComparison.OrdinalIgnoreCase))
                            ?? savedLayouts[0];

                        LayoutEngine.SetSlotRegions(layout.SlotRegions);
                        Debug.WriteLine($"Hotkey: Auto-loaded layout '{layout.Name}'");
                    }
                }

                // Auto-apply borderless if enabled and not already applied
                if (LayoutEngine.Options.MakeBorderless)
                {
                    var activeSlots = SlotManager.GetActiveSlots().ToList();
                    foreach (var slot in activeSlots)
                    {
                        if (slot.MainWindowHandle != IntPtr.Zero)
                        {
                            Multiboxer.Core.Window.WindowHelper.MakeBorderless(slot.MainWindowHandle);
                        }
                    }
                }

                // Apply layout
                if (LayoutEngine.SlotRegions.Count > 0)
                {
                    Debug.WriteLine($"Hotkey: Applying layout with main slot {slotId}");
                    LayoutEngine.ApplyLayoutWithMain(slotId);

                    // Apply thumbnails for background windows (JMB-style)
                    if (LayoutEngine.Options.UseThumbnails)
                    {
                        var activeSlots = SlotManager.GetActiveSlots().ToList();
                        // BackRegion stores ABSOLUTE screen coordinates - pass 0,0 offset
                        var monitor = GetTargetMonitor();
                        var bounds = monitor?.WorkingArea;
                        ThumbnailManager.ApplyLayout(activeSlots, LayoutEngine.SlotRegions, slotId, 0, 0, bounds);
                    }
                    else
                    {
                        ThumbnailManager.SetForegroundSlot(slotId);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in FocusAndApplyLayoutAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Focus next slot and apply layout
    /// </summary>
    private async Task FocusNextAndApplyLayoutAsync()
    {
        try
        {
            var success = SlotManager.FocusNextSlot();

            if (!success)
                return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Auto-load layout if needed
                if (LayoutEngine.SlotRegions.Count == 0)
                {
                    var savedLayouts = ConfigManager.Settings.SavedWindowLayouts;
                    if (savedLayouts != null && savedLayouts.Count > 0)
                    {
                        var layout = savedLayouts.FirstOrDefault(l =>
                            l.Name.Equals(ConfigManager.Settings.ActiveSavedLayoutName, StringComparison.OrdinalIgnoreCase))
                            ?? savedLayouts[0];
                        LayoutEngine.SetSlotRegions(layout.SlotRegions);
                    }
                }

                // Apply borderless if enabled
                if (LayoutEngine.Options.MakeBorderless)
                {
                    foreach (var slot in SlotManager.GetActiveSlots())
                    {
                        if (slot.MainWindowHandle != IntPtr.Zero)
                        {
                            Multiboxer.Core.Window.WindowHelper.MakeBorderless(slot.MainWindowHandle);
                        }
                    }
                }

                // Apply layout
                var focusedSlot = SlotManager.FocusedSlot;
                if (focusedSlot != null && LayoutEngine.SlotRegions.Count > 0)
                {
                    LayoutEngine.ApplyLayoutWithMain(focusedSlot.Id);

                    // Apply thumbnails for background windows (JMB-style)
                    if (LayoutEngine.Options.UseThumbnails)
                    {
                        var activeSlots = SlotManager.GetActiveSlots().ToList();
                        var monitor = GetTargetMonitor();
                        var bounds = monitor?.WorkingArea;
                        ThumbnailManager.ApplyLayout(activeSlots, LayoutEngine.SlotRegions, focusedSlot.Id, 0, 0, bounds);
                    }
                    else
                    {
                        ThumbnailManager.SetForegroundSlot(focusedSlot.Id);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in FocusNextAndApplyLayoutAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Load saved layout regions into the LayoutEngine so hotkeys work immediately
    /// </summary>
    private void LoadSavedLayoutRegions()
    {
        var savedLayouts = ConfigManager.Settings.SavedWindowLayouts;
        if (savedLayouts == null || savedLayouts.Count == 0)
        {
            Debug.WriteLine("No saved window layouts to load");
            return;
        }

        // Find the active layout by name, or use the first one if no active layout is set
        SavedWindowLayout? activeLayout = null;

        if (!string.IsNullOrEmpty(ConfigManager.Settings.ActiveSavedLayoutName))
        {
            activeLayout = savedLayouts.FirstOrDefault(l =>
                l.Name.Equals(ConfigManager.Settings.ActiveSavedLayoutName, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback to first layout if active one not found
        activeLayout ??= savedLayouts[0];

        if (activeLayout.SlotRegions.Count == 0)
        {
            Debug.WriteLine($"Saved layout '{activeLayout.Name}' has no regions");
            return;
        }

        // Load regions into LayoutEngine
        LayoutEngine.SetSlotRegions(activeLayout.SlotRegions);

        Debug.WriteLine($"Loaded {activeLayout.SlotRegions.Count} regions from saved layout '{activeLayout.Name}'");
    }

    /// <summary>
    /// Handle thumbnail click - switch to that slot
    /// </summary>
    private void OnThumbnailClicked(object? sender, Multiboxer.Overlay.ThumbnailClickEventArgs e)
    {
        Debug.WriteLine($"Thumbnail clicked: slot {e.SlotId}");
        _ = FocusAndApplyLayoutAsync(e.SlotId);
    }

    /// <summary>
    /// Apply custom window title (profile-driven with simple templating)
    /// </summary>
    private void ApplyCustomWindowTitle(Slot slot)
    {
        if (slot.MainWindowHandle == IntPtr.Zero)
            return;

        var profiles = ConfigManager.Settings.Profiles ?? new List<LaunchProfile>();
        var profile = profiles.FirstOrDefault(p => p.Name.Equals(slot.ProfileName, StringComparison.OrdinalIgnoreCase));

        var template = profile?.CustomWindowTitle;
        // Fallback: Slot {id}
        if (string.IsNullOrWhiteSpace(template))
        {
            template = $"Slot {slot.Id}";
        }

        var title = template
            .Replace("{slot}", slot.Id.ToString())
            .Replace("{profile}", slot.ProfileName ?? string.Empty)
            .Replace("{display}", slot.DisplayName ?? string.Empty);

        Multiboxer.Core.Window.WindowHelper.SetWindowTitle(slot.MainWindowHandle, title);
    }

    /// <summary>
    /// Remove thumbnails when a slot is removed
    /// </summary>
    private void OnSlotRemoved(object? sender, SlotEventArgs e)
    {
        ThumbnailManager.RemoveThumbnail(e.Slot.Id);
    }

    /// <summary>
    /// Remove thumbnails when a slot process exits
    /// </summary>
    private void OnSlotProcessExited(object? sender, SlotEventArgs e)
    {
        ThumbnailManager.RemoveThumbnail(e.Slot.Id);
    }

    /// <summary>
    /// Focus previous slot and apply layout
    /// </summary>
    private async Task FocusPreviousAndApplyLayoutAsync()
    {
        try
        {
            var success = SlotManager.FocusPreviousSlot();

            if (!success)
                return;

            if (LayoutEngine.Options.SwapOnHotkeyFocused)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LayoutEngine.ApplyLayout();

                    // Update thumbnails based on current foreground
                    var focusedSlot = SlotManager.FocusedSlot;
                    if (focusedSlot != null)
                    {
                        if (LayoutEngine.Options.UseThumbnails)
                        {
                            var activeSlots = SlotManager.GetActiveSlots().ToList();
                            var monitor = GetTargetMonitor();
                            var bounds = monitor?.WorkingArea;
                            ThumbnailManager.ApplyLayout(activeSlots, LayoutEngine.SlotRegions, focusedSlot.Id, 0, 0, bounds);
                        }
                        else
                        {
                            ThumbnailManager.SetForegroundSlot(focusedSlot.Id);
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in FocusPreviousAndApplyLayoutAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the target monitor for layouts based on settings
    /// </summary>
    private Multiboxer.Core.Window.MonitorInfo? GetTargetMonitor()
    {
        var monitorIndex = LayoutEngine.Options.MonitorIndex;
        if (monitorIndex >= 0)
        {
            var monitors = Multiboxer.Core.Window.MonitorManager.GetAllMonitors();
            if (monitorIndex < monitors.Count)
                return monitors[monitorIndex];
        }
        return Multiboxer.Core.Window.MonitorManager.GetPrimaryMonitor();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Save settings
        ConfigManager.Save();

        // Restore CPU affinities
        AffinityManager.RestoreAllOriginalAffinities();

        // Cleanup virtual files (restore original files)
        VirtualFileManager.CleanupAll();

        // Cleanup
        HotkeyManager.HotkeyPressed -= OnHotkeyPressed;
        SlotManager.SlotActivated -= OnSlotActivated;
        ThumbnailManager.ThumbnailClicked -= OnThumbnailClicked;
        SlotManager.SlotRemoved -= OnSlotRemoved;
        SlotManager.SlotProcessExited -= OnSlotProcessExited;
        HotkeyManager.Dispose();
        OverlayManager.Dispose();
        ThumbnailManager.Clear();
        TrayIconService.Dispose();
        SlotManager.Dispose();

        base.OnExit(e);
    }
}
