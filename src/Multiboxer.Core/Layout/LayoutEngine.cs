using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;
using Multiboxer.Native;

namespace Multiboxer.Core.Layout;

/// <summary>
/// ISBoxer-style layout engine.
///
/// Key concepts:
/// - Each slot has a ForeRegion (main/large) and BackRegion (thumbnail/small)
/// - When a slot is focused, it moves to ForeRegion
/// - When a slot loses focus, it moves to BackRegion
/// - Windows are pre-positioned to their regions, swapping is just Z-order + position
/// </summary>
public class LayoutEngine
{
    private readonly SlotManager _slotManager;
    private ILayoutStrategy _currentStrategy;
    private LayoutOptions _options;
    private MonitorInfo? _targetMonitor;

    // ISBoxer-style: Store regions for each slot
    private readonly Dictionary<int, SlotRegion> _slotRegions = new();
    // Template regions from the saved layout (by original slot ID)
    private List<(WindowRect ForeRegion, WindowRect BackRegion)> _templateRegions = new();
    private int _currentForegroundSlotId;
    private readonly HashSet<IntPtr> _protectedWindows = new();
    private readonly HashSet<IntPtr> _borderlessApplied = new();

    // Track if initial layout has been applied - subsequent focus changes only swap two windows
    private bool _initialLayoutApplied = false;
    // Track parked window positions to avoid redundant moves
    private readonly Dictionary<int, (int x, int y)> _parkedPositions = new();

    /// <summary>
    /// Available layout strategies
    /// </summary>
    public IReadOnlyDictionary<string, ILayoutStrategy> Strategies { get; }

    /// <summary>
    /// Current layout strategy
    /// </summary>
    public ILayoutStrategy CurrentStrategy
    {
        get => _currentStrategy;
        set
        {
            if (_currentStrategy != value)
            {
                _currentStrategy = value;
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Current layout options
    /// </summary>
    public LayoutOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            OptionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Custom layouts defined by the user
    /// </summary>
    public List<CustomLayout> CustomLayouts { get; } = new();

    /// <summary>
    /// Event raised when the layout changes
    /// </summary>
    public event EventHandler? LayoutChanged;

    /// <summary>
    /// Event raised when options change
    /// </summary>
    public event EventHandler? OptionsChanged;

    public LayoutEngine(SlotManager slotManager)
    {
        _slotManager = slotManager;
        _options = new LayoutOptions();

        // Initialize built-in strategies
        var horizontal = new HorizontalLayout();
        var vertical = new VerticalLayout();

        Strategies = new Dictionary<string, ILayoutStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { horizontal.Name, horizontal },
            { vertical.Name, vertical }
        };

        _currentStrategy = horizontal;

        // Subscribe to slot manager events
        _slotManager.ForegroundSlotChanged += OnForegroundSlotChanged;
    }

    /// <summary>
    /// Calculate and store regions for all active slots.
    /// Call this when slots are added/removed or layout changes.
    /// </summary>
    public void CalculateRegions()
    {
        var monitor = GetTargetMonitor();
        if (monitor == null)
            return;

        var bounds = _options.AvoidTaskbar ? monitor.WorkingArea : monitor.Bounds;
        var activeSlots = _slotManager.GetActiveSlots().ToList();

        if (activeSlots.Count == 0)
        {
            _slotRegions.Clear();
            return;
        }

        // Define the main (foreground) region - nearly full screen
        var foreRegion = new WindowRect(
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height - 150 // Leave space for thumbnail strip
        );

        // Calculate thumbnail positions for background regions
        int thumbnailWidth = 200;
        int thumbnailHeight = 150;
        int thumbnailY = bounds.Y + bounds.Height - thumbnailHeight;
        int currentX = bounds.X;

        _slotRegions.Clear();

        for (int i = 0; i < activeSlots.Count; i++)
        {
            var slot = activeSlots[i];
            var region = new SlotRegion
            {
                SlotId = slot.Id,
                ForeRegion = new WindowRect(foreRegion.X, foreRegion.Y, foreRegion.Width, foreRegion.Height),
                BackRegion = new WindowRect(currentX, thumbnailY, thumbnailWidth, thumbnailHeight),
                IsInForeground = (i == 0) // First slot starts in foreground
            };

            _slotRegions[slot.Id] = region;
            currentX += thumbnailWidth;
        }

        // Set initial foreground
        if (activeSlots.Count > 0 && _currentForegroundSlotId == 0)
        {
            _currentForegroundSlotId = activeSlots[0].Id;
        }
    }

    /// <summary>
    /// Apply the current layout to all active slots.
    /// Uses saved layout regions if available, otherwise calculates default regions.
    /// </summary>
    public void ApplyLayout()
    {
        var activeSlots = _slotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
            return;

        // If we have saved template regions, use those instead of recalculating
        // This preserves layouts created in Layout Manager
        if (_templateRegions.Count > 0)
        {
            // Ensure regions are mapped to current active slots
            RemapRegionsToSlots();
        }
        else
        {
            // No saved layout - use default calculated regions
            CalculateRegions();
        }

        // If still no regions, nothing to apply
        if (_slotRegions.Count == 0)
        {
        Debug.WriteLine("ApplyLayout: No regions to apply");
            return;
        }

        // Determine foreground slot
        if (_currentForegroundSlotId == 0 && activeSlots.Count > 0)
        {
            _currentForegroundSlotId = activeSlots[0].Id;
        }

        // Use the ISBoxer-style swap method which handles monitor offsets correctly
        SwapLayoutOnFocus(_currentForegroundSlotId);
    }

    /// <summary>
    /// Apply layout with a specific slot as the main (foreground) window.
    /// ISBoxer-style: Moves ALL windows to their correct regions based on focus.
    /// </summary>
    public void ApplyLayoutWithMain(int mainSlotId)
    {
        _currentForegroundSlotId = mainSlotId;

        var activeSlots = _slotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
            return;

        // If we have a template but not enough active slots yet, defer
        if (_templateRegions.Count > 0 && activeSlots.Count < _templateRegions.Count)
        {
            Debug.WriteLine($"ApplyLayoutWithMain: deferring, activeSlots={activeSlots.Count}, templateRegions={_templateRegions.Count}");
            return;
        }

        // If we have slot regions defined, do a full layout swap
        if (_slotRegions.Count > 0)
        {
            SwapLayoutOnFocus(mainSlotId);
            return;
        }

        // Fallback: just bring the target window to foreground
        var foregroundSlot = activeSlots.FirstOrDefault(s => s.Id == mainSlotId);
        if (foregroundSlot?.MainWindowHandle != IntPtr.Zero)
        {
            WindowHelper.ForceForegroundWindow(foregroundSlot.MainWindowHandle);
        }
    }

    /// <summary>
    /// ISBoxer-style layout swap: Move ALL windows to their correct regions based on focus.
    /// - Foreground slot goes to its ForeRegion (large main area)
    /// - All other slots go to their BackRegions (thumbnail strip)
    ///
    /// OPTIMIZATION: After initial layout, only swap the two affected windows (old foreground -> parked, new foreground -> ForeRegion)
    /// This avoids triggering resize events on windows that don't need to move.
    /// </summary>
    public void SwapLayoutOnFocus(int foregroundSlotId)
    {
        var previousForegroundSlotId = _currentForegroundSlotId;
        _currentForegroundSlotId = foregroundSlotId;

        var activeSlots = _slotManager.GetActiveSlots().ToList();

        Debug.WriteLine($"========== SwapLayoutOnFocus ==========");
        Debug.WriteLine($"  Foreground slot: {foregroundSlotId} (previous: {previousForegroundSlotId})");
        Debug.WriteLine($"  Active slots: {activeSlots.Count}");
        Debug.WriteLine($"  Slot regions: {_slotRegions.Count}");
        Debug.WriteLine($"  Template regions: {_templateRegions.Count}");
        Debug.WriteLine($"  MakeBorderless option: {_options.MakeBorderless}");
        Debug.WriteLine($"  Initial layout applied: {_initialLayoutApplied}");

        if (activeSlots.Count == 0)
        {
            Debug.WriteLine($"  ERROR: No active slots!");
            return;
        }

        // Ensure regions are mapped to current active slots
        RemapRegionsToSlots();

        // Defer if template requires more slots than we have
        if (_templateRegions.Count > 0 && activeSlots.Count < _templateRegions.Count)
        {
            Debug.WriteLine($"  Template requires {_templateRegions.Count} slots, only have {activeSlots.Count}; deferring layout");
            return;
        }

        if (_slotRegions.Count == 0)
        {
            Debug.WriteLine($"  ERROR: No slot regions defined!");
            return;
        }

        // Get target monitor offset - layouts are created with coordinates starting at 0,0
        var targetMonitor = GetTargetMonitor();
        int offsetX = targetMonitor?.WorkingArea.X ?? 0;
        int offsetY = targetMonitor?.WorkingArea.Y ?? 0;
        // Parking area: place real windows far off the virtual screen to keep them hidden while thumbnails show
        var virtualBounds = MonitorManager.GetVirtualScreenBounds();
        int parkingX = virtualBounds.Left - 2000;
        int parkingYStart = virtualBounds.Top - 2000;
        int parkingStep = 100;
        int parkingIndex = 0;

        Debug.WriteLine($"  Target monitor: {targetMonitor?.DeviceName ?? "null"} at ({offsetX},{offsetY}) size {targetMonitor?.Width}x{targetMonitor?.Height}");

        // OPTIMIZATION: If initial layout already applied and we're just switching focus, only move two windows
        if (_initialLayoutApplied && _options.UseThumbnails && previousForegroundSlotId != 0 && previousForegroundSlotId != foregroundSlotId)
        {
            SwapTwoWindows(previousForegroundSlotId, foregroundSlotId, activeSlots, offsetX, offsetY, parkingX, parkingYStart);
            return;
        }

        // Step 1: Make all windows borderless BEFORE positioning; track which still need it
        var borderlessPending = new List<IntPtr>();
        if (_options.MakeBorderless)
        {
            Debug.WriteLine($"  Applying borderless to {activeSlots.Count} windows...");
            foreach (var slot in activeSlots)
            {
                if (slot.MainWindowHandle == IntPtr.Zero)
                    continue;

                var hwnd = slot.MainWindowHandle;

                // Skip windows marked protected or already applied
                if (_protectedWindows.Contains(hwnd) || _borderlessApplied.Contains(hwnd))
                    continue;

                var success = WindowHelper.MakeBorderless(hwnd);
                var applied = success && WindowHelper.IsBorderlessApplied(hwnd);
                if (applied)
                {
                    _borderlessApplied.Add(hwnd);
                }
                else
                {
                    borderlessPending.Add(hwnd);
                    _protectedWindows.Add(hwnd);
                }
            }
            // Small delay to let style changes take effect before positioning
            System.Threading.Thread.Sleep(30);
        }

        // Step 2: Build list of window positions for batch update
        var windowPositions = new List<(IntPtr hwnd, int x, int y, int width, int height)>();

        Debug.WriteLine($"  Building window positions (UseThumbnails={_options.UseThumbnails})...");
        foreach (var slot in activeSlots)
        {
            if (slot.MainWindowHandle == IntPtr.Zero)
            {
                Debug.WriteLine($"    Slot {slot.Id}: hwnd=NULL, skipping");
                continue;
            }

            // Find the region for this slot
            if (!_slotRegions.TryGetValue(slot.Id, out var region))
            {
                Debug.WriteLine($"    Slot {slot.Id}: No region mapped! Available regions: [{string.Join(",", _slotRegions.Keys)}]");
                continue;
            }

            bool isForeground = slot.Id == foregroundSlotId;

            int finalX, finalY, finalWidth, finalHeight;

            // Use 1.0 scale to avoid oversizing on high DPI; thumbnails handle scaling visually
            var dpiScale = 1.0;

            if (isForeground)
            {
                // Restore if minimized, then position on-screen at ForeRegion
                if (WindowHelper.IsWindowMinimized(slot.MainWindowHandle))
                {
                    Native.User32.ShowWindow(slot.MainWindowHandle, ShowWindowCommand.SW_RESTORE);
                }

                finalX = region.ForeRegion.X + offsetX;
                finalY = region.ForeRegion.Y + offsetY;
                finalWidth = (int)Math.Round(region.ForeRegion.Width * dpiScale);
                finalHeight = (int)Math.Round(region.ForeRegion.Height * dpiScale);
                Debug.WriteLine($"    Slot {slot.Id} (hwnd=0x{slot.MainWindowHandle:X}): FOREGROUND at ({finalX},{finalY}) {finalWidth}x{finalHeight}");

                windowPositions.Add((slot.MainWindowHandle, finalX, finalY, finalWidth, finalHeight));
            }
            else if (_options.UseThumbnails)
            {
                // Background window with thumbnails: park off-screen to avoid overlapping visible monitors
                finalX = parkingX;
                finalY = parkingYStart + parkingIndex * parkingStep;
                finalWidth = (int)Math.Round(region.ForeRegion.Width * dpiScale);
                finalHeight = (int)Math.Round(region.ForeRegion.Height * dpiScale);
                parkingIndex++;
                Debug.WriteLine($"    Slot {slot.Id} (hwnd=0x{slot.MainWindowHandle:X}): background (thumbnail mode) parked at ({finalX},{finalY}) {finalWidth}x{finalHeight}");

                windowPositions.Add((slot.MainWindowHandle, finalX, finalY, finalWidth, finalHeight));
            }
            else
            {
                // Background window without thumbnails: position at BackRegion (actual resize)
                finalX = region.BackRegion.X + offsetX;
                finalY = region.BackRegion.Y + offsetY;
                finalWidth = (int)Math.Round(region.BackRegion.Width * dpiScale);
                finalHeight = (int)Math.Round(region.BackRegion.Height * dpiScale);
                Debug.WriteLine($"    Slot {slot.Id} (hwnd=0x{slot.MainWindowHandle:X}): background at ({finalX},{finalY}) {finalWidth}x{finalHeight}");

                windowPositions.Add((slot.MainWindowHandle, finalX, finalY, finalWidth, finalHeight));
            }
        }

        // Step 3: Apply all window positions atomically using DeferWindowPos
        if (windowPositions.Count > 0)
        {
            Debug.WriteLine($"  Applying {windowPositions.Count} window positions...");
            WindowHelper.SetWindowPositionsBatched(windowPositions);
        }
        else
        {
            Debug.WriteLine($"  WARNING: No window positions to apply!");
        }

        // Step 4: Bring foreground window to top
        var foregroundSlot = activeSlots.FirstOrDefault(s => s.Id == foregroundSlotId);
        if (foregroundSlot?.MainWindowHandle != IntPtr.Zero)
        {
            Debug.WriteLine($"  Bringing slot {foregroundSlotId} to foreground");
            WindowHelper.ForceForegroundWindow(foregroundSlot.MainWindowHandle);
        }

        // Update slot window info
        foreach (var slot in activeSlots)
        {
            slot.UpdateWindowInfo();
        }

        // Mark initial layout as applied - subsequent focus changes will use optimized path
        _initialLayoutApplied = true;

        // Store parked positions for optimization
        foreach (var slot in activeSlots)
        {
            if (slot.Id != foregroundSlotId && _options.UseThumbnails)
            {
                // Find the parked position we used
                var idx = activeSlots.Where(s => s.Id != foregroundSlotId).ToList().IndexOf(slot);
                if (idx >= 0)
                {
                    _parkedPositions[slot.Id] = (parkingX, parkingYStart + idx * parkingStep);
                }
            }
        }

        Debug.WriteLine($"========== SwapLayoutOnFocus COMPLETE ==========");
    }

    /// <summary>
    /// JMB-style optimized swap: Batch move two windows atomically using DeferWindowPos.
    /// - Previous foreground -> parked position (off-screen)
    /// - New foreground -> ForeRegion (on-screen) and brought to top via z-order
    /// Uses SWP_NOSIZE to avoid resize events.
    /// </summary>
    private void SwapTwoWindows(int previousSlotId, int newSlotId, List<Slot> activeSlots, int offsetX, int offsetY, int parkingX, int parkingYStart)
    {
        Debug.WriteLine($"=== SwapTwoWindows: {previousSlotId} -> {newSlotId} ===");

        var previousSlot = activeSlots.FirstOrDefault(s => s.Id == previousSlotId);
        var newSlot = activeSlots.FirstOrDefault(s => s.Id == newSlotId);

        if (previousSlot?.MainWindowHandle == IntPtr.Zero && newSlot?.MainWindowHandle == IntPtr.Zero)
        {
            Debug.WriteLine("  ERROR: Both slot handles are null!");
            return;
        }

        // Get regions
        _slotRegions.TryGetValue(newSlotId, out var newRegion);

        if (newRegion == null)
        {
            Debug.WriteLine($"  ERROR: No region for new slot {newSlotId}");
            return;
        }

        // Calculate positions
        int parkedIdx = activeSlots.Where(s => s.Id != newSlotId).ToList().FindIndex(s => s.Id == previousSlotId);
        int parkedX = parkingX;
        int parkedY = parkingYStart + (parkedIdx >= 0 ? parkedIdx : 0) * 100;
        int foreX = newRegion.ForeRegion.X + offsetX;
        int foreY = newRegion.ForeRegion.Y + offsetY;

        // Restore new window if minimized (before batching)
        if (newSlot?.MainWindowHandle != IntPtr.Zero && WindowHelper.IsWindowMinimized(newSlot.MainWindowHandle))
        {
            User32.ShowWindow(newSlot.MainWindowHandle, ShowWindowCommand.SW_RESTORE);
        }

        // JMB-style: Batch both moves in a single DeferWindowPos for atomic update
        var hdwp = User32.BeginDeferWindowPos(2);
        if (hdwp == IntPtr.Zero)
        {
            Debug.WriteLine("  BeginDeferWindowPos failed, using fallback");
            // Fallback to individual calls
            FallbackSwapTwoWindows(previousSlot, newSlot, parkedX, parkedY, foreX, foreY, previousSlotId, newSlotId);
            return;
        }

        // Flags: NOSIZE (no resize), DEFERERASE (reduce flicker), ASYNCWINDOWPOS (async)
        var parkFlags = SetWindowPosFlags.SWP_NOSIZE |
                        SetWindowPosFlags.SWP_NOZORDER |
                        SetWindowPosFlags.SWP_NOACTIVATE |
                        SetWindowPosFlags.SWP_DEFERERASE |
                        SetWindowPosFlags.SWP_ASYNCWINDOWPOS;

        // Move previous foreground to parked (keep z-order, no activate)
        if (previousSlot?.MainWindowHandle != IntPtr.Zero)
        {
            Debug.WriteLine($"  Deferring slot {previousSlotId} to parked ({parkedX},{parkedY})");
            hdwp = User32.DeferWindowPos(hdwp, previousSlot.MainWindowHandle, IntPtr.Zero,
                parkedX, parkedY, 0, 0, parkFlags);
            _parkedPositions[previousSlotId] = (parkedX, parkedY);
        }

        // Move new foreground to ForeRegion AND bring to top (HWND_TOP via z-order)
        if (newSlot?.MainWindowHandle != IntPtr.Zero && hdwp != IntPtr.Zero)
        {
            // For foreground: use HWND_TOP to bring to front, no NOZORDER
            var foreFlags = SetWindowPosFlags.SWP_NOSIZE |
                            SetWindowPosFlags.SWP_NOACTIVATE |
                            SetWindowPosFlags.SWP_DEFERERASE |
                            SetWindowPosFlags.SWP_ASYNCWINDOWPOS;

            Debug.WriteLine($"  Deferring slot {newSlotId} to ForeRegion ({foreX},{foreY}) with HWND_TOP");
            hdwp = User32.DeferWindowPos(hdwp, newSlot.MainWindowHandle, User32.HWND_TOP,
                foreX, foreY, 0, 0, foreFlags);
        }

        // Execute all moves atomically
        if (hdwp != IntPtr.Zero)
        {
            User32.EndDeferWindowPos(hdwp);
        }

        // Final step: Activate the window (separate from positioning for smoothness)
        if (newSlot?.MainWindowHandle != IntPtr.Zero)
        {
            User32.SetForegroundWindow(newSlot.MainWindowHandle);
        }

        Debug.WriteLine($"=== SwapTwoWindows COMPLETE ===");
    }

    /// <summary>
    /// Fallback for when DeferWindowPos fails
    /// </summary>
    private void FallbackSwapTwoWindows(Slot? previousSlot, Slot? newSlot, int parkedX, int parkedY, int foreX, int foreY, int previousSlotId, int newSlotId)
    {
        var flags = SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE;

        if (previousSlot?.MainWindowHandle != IntPtr.Zero)
        {
            User32.SetWindowPos(previousSlot.MainWindowHandle, IntPtr.Zero, parkedX, parkedY, 0, 0, flags);
            _parkedPositions[previousSlotId] = (parkedX, parkedY);
        }

        if (newSlot?.MainWindowHandle != IntPtr.Zero)
        {
            User32.SetWindowPos(newSlot.MainWindowHandle, User32.HWND_TOP, foreX, foreY, 0, 0,
                SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            User32.SetForegroundWindow(newSlot.MainWindowHandle);
        }
    }

    /// <summary>
    /// Handle foreground slot changes
    /// </summary>
    private void OnForegroundSlotChanged(object? sender, SlotEventArgs e)
    {
        // Layout is applied by the caller (App.xaml.cs) for hotkeys
        // This event is for notification only to prevent double application
    }

    /// <summary>
    /// Get the target monitor for layouts
    /// </summary>
    private MonitorInfo? GetTargetMonitor()
    {
        if (_targetMonitor != null)
            return _targetMonitor;

        if (_options.MonitorIndex >= 0)
        {
            var monitors = MonitorManager.GetAllMonitors();
            if (_options.MonitorIndex < monitors.Count)
                return monitors[_options.MonitorIndex];
        }

        // Default to primary monitor
        return MonitorManager.GetPrimaryMonitor();
    }

    /// <summary>
    /// Set a specific monitor as the target
    /// </summary>
    public void SetTargetMonitor(MonitorInfo? monitor)
    {
        _targetMonitor = monitor;
    }

    /// <summary>
    /// Set layout strategy by name
    /// </summary>
    public bool SetStrategy(string name)
    {
        // Check built-in strategies
        if (Strategies.TryGetValue(name, out var strategy))
        {
            CurrentStrategy = strategy;
            return true;
        }

        // Check custom layouts
        var customLayout = CustomLayouts.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (customLayout != null)
        {
            CurrentStrategy = customLayout;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Add a custom layout
    /// </summary>
    public void AddCustomLayout(CustomLayout layout)
    {
        // Remove existing with same name
        CustomLayouts.RemoveAll(c => c.Name.Equals(layout.Name, StringComparison.OrdinalIgnoreCase));
        CustomLayouts.Add(layout);
    }

    /// <summary>
    /// Remove a custom layout by name
    /// </summary>
    public bool RemoveCustomLayout(string name)
    {
        return CustomLayouts.RemoveAll(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    /// <summary>
    /// Get all available layout names (built-in + custom)
    /// </summary>
    public IEnumerable<string> GetAvailableLayouts()
    {
        return Strategies.Keys.Concat(CustomLayouts.Select(c => c.Name));
    }

    /// <summary>
    /// Preview layout regions without applying
    /// </summary>
    public IReadOnlyList<WindowRegion> PreviewLayout(int slotCount)
    {
        var monitor = GetTargetMonitor();
        if (monitor == null)
            return Array.Empty<WindowRegion>();

        return _currentStrategy.CalculateRegions(slotCount, monitor, _options);
    }

    /// <summary>
    /// Get the current foreground slot ID
    /// </summary>
    public int CurrentForegroundSlotId => _currentForegroundSlotId;

    /// <summary>
    /// Get regions for a specific slot
    /// </summary>
    public SlotRegion? GetSlotRegion(int slotId)
    {
        return _slotRegions.TryGetValue(slotId, out var region) ? region : null;
    }

    /// <summary>
    /// Set slot regions from a saved layout (e.g., from Layout Manager)
    /// Stores template and maps regions to active slots by index
    /// </summary>
    public void SetSlotRegions(IEnumerable<SlotRegion> regions)
    {
        var regionList = regions.OrderBy(r => r.SlotId).ToList();

        // Store as template for remapping later
        _templateRegions = regionList
            .Select(r => (r.ForeRegion, r.BackRegion))
            .ToList();

        // Now map to current active slots
        RemapRegionsToSlots();
    }

    /// <summary>
    /// Remap layout regions to current active slots
    /// Call this after slots are added/removed
    /// </summary>
    public void RemapRegionsToSlots()
    {
        if (_templateRegions.Count == 0)
            return;

        // Get active slots sorted by ID
        var activeSlots = _slotManager.GetActiveSlots().OrderBy(s => s.Id).ToList();

        // Clear and remap
        _slotRegions.Clear();

        for (int i = 0; i < Math.Min(_templateRegions.Count, activeSlots.Count); i++)
        {
            var slot = activeSlots[i];
            var (foreRegion, backRegion) = _templateRegions[i];

            _slotRegions[slot.Id] = new SlotRegion
            {
                SlotId = slot.Id,
                ForeRegion = foreRegion,
                BackRegion = backRegion
            };
        }

        Debug.WriteLine($"RemapRegionsToSlots: Mapped {_slotRegions.Count} regions to slots");
    }

    /// <summary>
    /// Get number of template regions (layout capacity)
    /// </summary>
    public int TemplateRegionCount => _templateRegions.Count;

    /// <summary>
    /// Get all slot regions
    /// </summary>
    public IReadOnlyDictionary<int, SlotRegion> SlotRegions => _slotRegions;

    /// <summary>
    /// Whether we have a full template and enough active slots to use it
    /// </summary>
    public bool HasEnoughSlotsForTemplate(int activeSlotCount)
    {
        if (_templateRegions.Count == 0)
            return true;
        return activeSlotCount >= _templateRegions.Count;
    }
}
