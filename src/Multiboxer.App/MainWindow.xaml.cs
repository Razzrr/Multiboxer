using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Multiboxer.App.Views;
using Multiboxer.Core.Config;
using Multiboxer.Core.Input;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;

namespace Multiboxer.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool _minimizeToTray = true;
    private CancellationTokenSource? _batchLaunchCts;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Bind slots list
        SlotsListView.ItemsSource = App.SlotManager.ActiveSlots;

        // Bind hotkeys list
        HotkeysListView.ItemsSource = new ObservableCollection<HotkeyBinding>(App.HotkeyManager.Bindings.Values);

        // Load profiles
        LoadProfiles();

        // Load layout options
        LoadLayoutComboBox();
        LoadMonitorComboBox();
        LoadLayoutOptions();

        // Load highlighter settings
        LoadHighlighterSettings();

        // Load general settings
        LoadGeneralSettings();

        // Subscribe to slot manager events
        App.SlotManager.SlotAdded += SlotManager_SlotChanged;
        App.SlotManager.SlotRemoved += SlotManager_SlotChanged;
        App.SlotManager.SlotProcessExited += SlotManager_SlotChanged;

        // Subscribe to tray icon events
        App.TrayIconService.ShowWindowRequested += (s, e) => ShowAndActivate();
        App.TrayIconService.ExitRequested += (s, e) => ExitApplication();

        UpdateStatusBar();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Unsubscribe from events
        App.SlotManager.SlotAdded -= SlotManager_SlotChanged;
        App.SlotManager.SlotRemoved -= SlotManager_SlotChanged;
        App.SlotManager.SlotProcessExited -= SlotManager_SlotChanged;

        // Save settings
        SaveSettings();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _minimizeToTray)
        {
            Hide();
            App.TrayIconService.ShowNotification("Multiboxer", "Minimized to system tray. Double-click to restore.");
        }
    }

    private void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _minimizeToTray = false; // Prevent minimize on close
        Application.Current.Shutdown();
    }

    private void LoadProfiles()
    {
        var profiles = App.ConfigManager.Settings.Profiles ?? new List<LaunchProfile>();
        ProfileComboBox.ItemsSource = profiles;

        if (profiles.Count > 0)
        {
            ProfileComboBox.SelectedIndex = 0;
        }
    }

    private void LoadLayoutComboBox()
    {
        var savedLayouts = App.ConfigManager.Settings.SavedWindowLayouts ?? new List<SavedWindowLayout>();
        var layoutNames = savedLayouts.Select(l => l.Name).ToList();

        if (layoutNames.Count == 0)
        {
            layoutNames.Add("(No layouts - use Layout Manager)");
        }

        LaunchLayoutComboBox.ItemsSource = layoutNames;

        // Select the active saved layout, or first one
        var activeLayoutName = App.ConfigManager.Settings.ActiveSavedLayoutName;
        var activeIndex = layoutNames.FindIndex(n => n.Equals(activeLayoutName, StringComparison.OrdinalIgnoreCase));
        LaunchLayoutComboBox.SelectedIndex = activeIndex >= 0 ? activeIndex : 0;
    }

    private void LoadMonitorComboBox()
    {
        var monitors = MonitorManager.GetAllMonitors();
        var items = new List<string> { "Primary Monitor" };
        items.AddRange(monitors.Select(m => $"Monitor {m.Index + 1}: {m.Width}x{m.Height}{(m.IsPrimary ? " (Primary)" : "")}"));
        LaunchMonitorComboBox.ItemsSource = items;
        // Reflect saved monitor selection
        var savedIndex = App.LayoutEngine.Options.MonitorIndex;
        var selected = savedIndex < 0 ? 0 : savedIndex + 1;
        if (selected < 0 || selected >= items.Count)
            selected = 0;
        LaunchMonitorComboBox.SelectedIndex = selected;
        // Apply immediately so runtime matches UI
        ApplyMonitorSelection(selected);
    }

    private void LoadLayoutOptions()
    {
        var options = App.LayoutEngine.Options;
        // Sync launcher checkboxes with saved options
        LaunchBorderlessCheckBox.IsChecked = options.MakeBorderless;
        LaunchAutoLayoutCheckBox.IsChecked = true; // Always auto-layout by default
    }

    private void LoadHighlighterSettings()
    {
        ShowBorderCheckBox.IsChecked = App.OverlayManager.ShowBorder;
        ShowNumberCheckBox.IsChecked = App.OverlayManager.ShowNumber;
    }

    private void LoadGeneralSettings()
    {
        _minimizeToTray = App.ConfigManager.Settings.Window?.MinimizeToTray ?? true;
        MinimizeToTrayCheckBox.IsChecked = _minimizeToTray;
        StartMinimizedCheckBox.IsChecked = App.ConfigManager.Settings.Window?.StartMinimized ?? false;

        // Load affinity setting
        LockAffinityCheckBox.IsChecked = App.ConfigManager.Settings.Performance?.LockAffinity ?? false;
    }

    private void SaveSettings()
    {
        // Save layout options
        App.ConfigManager.Settings.Layout = new LayoutSettings
        {
            ActiveLayout = App.LayoutEngine.CurrentStrategy.Name,
            Options = App.LayoutEngine.Options
        };

        // Save highlighter settings
        App.ConfigManager.Settings.Highlighter = new HighlighterSettings
        {
            ShowBorder = App.OverlayManager.ShowBorder,
            ShowNumber = App.OverlayManager.ShowNumber
        };

        // Save window settings
        if (App.ConfigManager.Settings.Window == null)
        {
            App.ConfigManager.Settings.Window = new WindowSettings();
        }
        App.ConfigManager.Settings.Window.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
        App.ConfigManager.Settings.Window.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;

        // Save performance settings
        if (App.ConfigManager.Settings.Performance == null)
        {
            App.ConfigManager.Settings.Performance = new PerformanceSettings();
        }
        App.ConfigManager.Settings.Performance.LockAffinity = LockAffinityCheckBox.IsChecked ?? false;

        App.ConfigManager.Save();
    }

    private void UpdateStatusBar()
    {
        // Use BeginInvoke to avoid deadlocks - it's async and doesn't block
        Dispatcher.BeginInvoke(() =>
        {
            ActiveSlotsCount.Text = App.SlotManager.ActiveSlotCount.ToString();
            App.TrayIconService.UpdateTooltip($"Multiboxer - {App.SlotManager.ActiveSlotCount} active slots");
        });
    }

    private void SlotManager_SlotChanged(object? sender, SlotEventArgs e)
    {
        // Use BeginInvoke to avoid blocking the calling thread
        Dispatcher.BeginInvoke(() =>
        {
            UpdateStatusBar();
            App.TrayIconService.UpdateContextMenu();
        });
    }

    #region Launcher Tab Events

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        var profile = ProfileComboBox.SelectedItem as LaunchProfile;
        if (profile == null)
        {
            MessageBox.Show("Please select a launch profile.", "No Profile Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!profile.IsValid())
        {
            MessageBox.Show($"Profile '{profile.Name}' is invalid. Please check the path and executable.",
                "Invalid Profile", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Sync launch settings with layout engine
        App.LayoutEngine.Options.MakeBorderless = LaunchBorderlessCheckBox.IsChecked ?? true;

        // Ensure monitor selection is applied
        ApplyMonitorSelection(LaunchMonitorComboBox.SelectedIndex);

        // Load selected layout if available
        var savedLayouts = App.ConfigManager.Settings.SavedWindowLayouts ?? new List<SavedWindowLayout>();
        var layoutIndex = LaunchLayoutComboBox.SelectedIndex;
        if (layoutIndex >= 0 && layoutIndex < savedLayouts.Count)
        {
            var selectedLayout = savedLayouts[layoutIndex];
            App.LayoutEngine.SetSlotRegions(selectedLayout.SlotRegions);
        }

        try
        {
            LaunchButton.IsEnabled = false;
            StatusBarText.Text = $"Launching {profile.Name}...";

            // Get next available slot ID for virtual file setup
            var nextSlotId = App.SlotManager.GetNextAvailableSlotId();
            if (nextSlotId < 0)
            {
                StatusBarText.Text = "Launch failed - no available slots";
                MessageBox.Show("No available slots. Maximum is 40.", "Launch Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Setup virtual files before launching (if enabled)
            if (profile.UseVirtualFiles && profile.VirtualFiles.Count > 0)
            {
                if (!App.VirtualFileManager.SetupVirtualFiles(nextSlotId, profile))
                {
                    var result = MessageBox.Show(
                        "Virtual file setup failed. This may require administrator privileges.\nContinue launch without virtual files?",
                        "Virtual Files",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        StatusBarText.Text = "Launch cancelled";
                        return;
                    }
                }
            }

            // Add launch delay if configured
            if (profile.LaunchDelay > 0)
            {
                await Task.Delay(profile.LaunchDelay);
            }

            var slot = await App.SlotManager.LaunchAsync(profile);

            if (slot != null)
            {
                StatusBarText.Text = $"Launched {profile.Name} in slot {slot.Id}";

                // Apply CPU affinity if enabled
                if (LockAffinityCheckBox.IsChecked == true && slot.Process != null)
                {
                    App.AffinityManager.PinToCore(slot.Process, (slot.Id - 1) % App.AffinityManager.ProcessorCount);
                }

                // Apply layout after a short delay to let the window initialize
                await Task.Delay(500);
                App.LayoutEngine.ApplyLayout();

                App.TrayIconService.ShowNotification("Launched", $"{profile.Name} started in slot {slot.Id}");
            }
            else
            {
                StatusBarText.Text = "Launch failed - no available slots";
                MessageBox.Show("No available slots. Maximum is 40.", "Launch Failed", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Cleanup virtual files on failed launch
                App.VirtualFileManager.CleanupSlotSymlinks(nextSlotId);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusBarText.Text = "Launch failed";
        }
        finally
        {
            LaunchButton.IsEnabled = true;
        }
    }

    private void AttachWindow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WindowPickerDialog { Owner = this };

        if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
        {
            try
            {
                var process = Process.GetProcessById(dialog.SelectedWindow.ProcessId);
                var slot = App.SlotManager.AttachProcess(dialog.SelectedSlotId, process);

                StatusBarText.Text = $"Attached '{dialog.SelectedWindow.Title}' to slot {slot.Id}";

                // Apply layout
                App.LayoutEngine.ApplyLayout();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to attach window: {ex.Message}", "Attach Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedSlot = SlotsListView.SelectedItem as Slot;
        if (selectedSlot != null)
        {
            selectedSlot.Close();
            App.SlotManager.RemoveSlot(selectedSlot.Id);
            StatusBarText.Text = $"Closed slot {selectedSlot.Id}";
        }
    }

    private void CloseAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to close all slots?",
            "Confirm Close All", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            App.SlotManager.CloseAll();
            StatusBarText.Text = "All slots closed";
        }
    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileManagerDialog { Owner = this };
        dialog.ShowDialog();

        // Reload profiles after dialog closes
        LoadProfiles();
    }

    private async void BatchLaunch_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.Tag == null)
            return;

        if (!int.TryParse(button.Tag.ToString(), out int count))
            return;

        var profile = ProfileComboBox.SelectedItem as LaunchProfile;
        if (profile == null)
        {
            MessageBox.Show("Please select a launch profile.", "No Profile Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!profile.IsValid())
        {
            MessageBox.Show($"Profile '{profile.Name}' is invalid. Please check the path and executable.",
                "Invalid Profile", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Sync launch settings with layout engine
        App.LayoutEngine.Options.MakeBorderless = LaunchBorderlessCheckBox.IsChecked ?? true;

        // Ensure monitor selection is applied
        ApplyMonitorSelection(LaunchMonitorComboBox.SelectedIndex);

        // Make sure a layout is selected and loaded
        var savedLayouts = App.ConfigManager.Settings.SavedWindowLayouts ?? new List<SavedWindowLayout>();
        var layoutIndex = LaunchLayoutComboBox.SelectedIndex;
        if (layoutIndex >= 0 && layoutIndex < savedLayouts.Count)
        {
            var selectedLayout = savedLayouts[layoutIndex];
            App.LayoutEngine.SetSlotRegions(selectedLayout.SlotRegions);
            Debug.WriteLine($"Batch launch: Using layout '{selectedLayout.Name}' with {selectedLayout.SlotRegions.Count} regions");
        }
        else if (App.LayoutEngine.SlotRegions.Count == 0)
        {
            MessageBox.Show("Please select a window layout before launching.\n\nUse the Layout Manager to create layouts.",
                "No Layout Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Disable batch buttons and enable cancel
        SetBatchButtonsEnabled(false);
        CancelBatchButton.IsEnabled = true;
        _batchLaunchCts = new CancellationTokenSource();

        // Set flag to prevent per-slot layout updates during batch launch
        App.IsBatchLaunching = true;

        try
        {
            for (int i = 1; i <= count; i++)
            {
                if (_batchLaunchCts.Token.IsCancellationRequested)
                {
                    BatchStatusText.Text = $"Cancelled after launching {i - 1} sessions";
                    break;
                }

                BatchStatusText.Text = $"Launching session {i} of {count}...";
                StatusBarText.Text = $"Batch launching {profile.Name} ({i}/{count})...";

                try
                {
                    var slot = await App.SlotManager.LaunchAsync(profile, _batchLaunchCts.Token);

                    if (slot != null)
                    {
                        // Apply CPU affinity if enabled
                        if (LockAffinityCheckBox.IsChecked == true && slot.Process != null)
                        {
                            App.AffinityManager.PinToCore(slot.Process, (slot.Id - 1) % App.AffinityManager.ProcessorCount);
                        }
                    }
                    else
                    {
                        BatchStatusText.Text = $"Failed to launch session {i} - no available slots";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    BatchStatusText.Text = $"Error launching session {i}: {ex.Message}";
                    break;
                }

                // Wait 3 seconds before next launch (unless this is the last one)
                if (i < count && !_batchLaunchCts.Token.IsCancellationRequested)
                {
                    for (int s = 3; s > 0; s--)
                    {
                        if (_batchLaunchCts.Token.IsCancellationRequested)
                            break;
                        BatchStatusText.Text = $"Launched {i}/{count}. Next launch in {s}s...";
                        await Task.Delay(1000, _batchLaunchCts.Token);
                    }
                }
            }

            if (!_batchLaunchCts.Token.IsCancellationRequested)
            {
                BatchStatusText.Text = $"Completed! Launched {count} sessions.";
                StatusBarText.Text = $"Batch launch complete - {count} sessions";

                // Apply layout after all launches
                await Task.Delay(500);
                App.LayoutEngine.ApplyLayout();

                App.TrayIconService.ShowNotification("Batch Launch Complete", $"Launched {count} {profile.Name} sessions");
            }
        }
        catch (OperationCanceledException)
        {
            BatchStatusText.Text = "Batch launch cancelled";
        }
        finally
        {
            // Clear batch launch flag
            App.IsBatchLaunching = false;

            SetBatchButtonsEnabled(true);
            CancelBatchButton.IsEnabled = false;
            _batchLaunchCts?.Dispose();
            _batchLaunchCts = null;
        }
    }

    private void CancelBatch_Click(object sender, RoutedEventArgs e)
    {
        _batchLaunchCts?.Cancel();
        BatchStatusText.Text = "Cancelling...";
    }

    private void SetBatchButtonsEnabled(bool enabled)
    {
        // Enable/disable all batch launch buttons and the single launch button
        LaunchButton.IsEnabled = enabled;

        // Find the batch launch GroupBox and disable all buttons inside except Cancel
        var batchGroupBox = CancelBatchButton.Parent as System.Windows.Controls.WrapPanel;
        if (batchGroupBox != null)
        {
            foreach (var child in batchGroupBox.Children)
            {
                if (child is System.Windows.Controls.Button btn && btn != CancelBatchButton)
                {
                    btn.IsEnabled = enabled;
                }
            }
        }
    }

    #endregion

    #region Layout Events

    private void LaunchLayoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var selectedIndex = LaunchLayoutComboBox.SelectedIndex;
        var savedLayouts = App.ConfigManager.Settings.SavedWindowLayouts ?? new List<SavedWindowLayout>();

        if (selectedIndex >= 0 && selectedIndex < savedLayouts.Count)
        {
            var selectedLayout = savedLayouts[selectedIndex];
            App.ConfigManager.Settings.ActiveSavedLayoutName = selectedLayout.Name;

            // Load the layout regions into the layout engine
            App.LayoutEngine.SetSlotRegions(selectedLayout.SlotRegions);

            StatusBarText.Text = $"Layout: {selectedLayout.Name}";
            LayoutStatusText.Text = $"{selectedLayout.SlotRegions.Count} slots defined";
        }
    }

    private void LaunchMonitorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ApplyMonitorSelection(LaunchMonitorComboBox.SelectedIndex);
        StatusBarText.Text = $"Target monitor changed";
    }

    private void ApplyMonitorSelection(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            // Primary monitor
            App.LayoutEngine.Options.MonitorIndex = -1; // Use primary
            App.LayoutEngine.SetTargetMonitor(null);
        }
        else
        {
            // Specific monitor
            var monitors = MonitorManager.GetAllMonitors();
            var monitorIndex = selectedIndex - 1;
            if (monitorIndex < monitors.Count)
            {
                App.LayoutEngine.Options.MonitorIndex = monitorIndex;
                App.LayoutEngine.SetTargetMonitor(monitors[monitorIndex]);
            }
        }
    }

    private void EditCustomLayout_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CustomLayoutEditorDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            // Reload layouts
            LoadLayoutComboBox();

            // If a new layout was created, select it
            if (dialog.ResultLayout != null)
            {
                var index = LaunchLayoutComboBox.Items.IndexOf(dialog.ResultLayout.Name);
                if (index >= 0)
                    LaunchLayoutComboBox.SelectedIndex = index;
            }
        }
    }

    private void LayoutManager_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WindowLayoutManagerDialog { Owner = this };
        dialog.ShowDialog();

        // Refresh the layout combo after Layout Manager closes
        LoadLayoutComboBox();
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        var activeSlots = App.SlotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
        {
            StatusBarText.Text = "No active windows to apply layout";
            LayoutStatusText.Text = "Launch windows first";
            return;
        }

        // Ensure monitor selection is applied
        ApplyMonitorSelection(LaunchMonitorComboBox.SelectedIndex);

        // Load the selected layout
        var savedLayouts = App.ConfigManager.Settings.SavedWindowLayouts ?? new List<SavedWindowLayout>();
        var layoutIndex = LaunchLayoutComboBox.SelectedIndex;
        if (layoutIndex >= 0 && layoutIndex < savedLayouts.Count)
        {
            var selectedLayout = savedLayouts[layoutIndex];
            App.LayoutEngine.SetSlotRegions(selectedLayout.SlotRegions);
        }

        if (App.LayoutEngine.SlotRegions.Count == 0)
        {
            StatusBarText.Text = "No layout selected";
            LayoutStatusText.Text = "Select a layout first";
            return;
        }

        // Sync borderless setting
        App.LayoutEngine.Options.MakeBorderless = LaunchBorderlessCheckBox.IsChecked ?? true;

        // Make windows borderless first if enabled
        if (App.LayoutEngine.Options.MakeBorderless)
        {
            foreach (var slot in activeSlots)
            {
                if (slot.MainWindowHandle != IntPtr.Zero)
                {
                    WindowHelper.MakeBorderless(slot.MainWindowHandle);
                }
            }
            // Short delay for style changes to take effect
            System.Threading.Thread.Sleep(100);
        }

        // Apply layout with first slot as foreground
        var foregroundId = App.LayoutEngine.CurrentForegroundSlotId;
        if (foregroundId == 0 && activeSlots.Count > 0)
            foregroundId = activeSlots[0].Id;

        App.LayoutEngine.ApplyLayoutWithMain(foregroundId);

        StatusBarText.Text = $"Layout applied to {activeSlots.Count} windows";
        LayoutStatusText.Text = "Layout applied";
    }

    #endregion

    #region Settings Tab Events

    private void HighlighterSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        App.OverlayManager.ShowBorder = ShowBorderCheckBox.IsChecked ?? true;
        App.OverlayManager.ShowNumber = ShowNumberCheckBox.IsChecked ?? true;
    }

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Settings",
            Filter = "JSON files (*.json)|*.json",
            FileName = "multiboxer-settings.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                SaveSettings(); // Save current state first
                App.ConfigManager.Export(dialog.FileName);
                MessageBox.Show($"Settings exported to {dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export settings: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Settings",
            Filter = "JSON files (*.json)|*.json",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            var result = MessageBox.Show(
                "This will replace all current settings. Are you sure?",
                "Confirm Import",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (App.ConfigManager.Import(dialog.FileName))
                    {
                        // Reload UI with new settings
                        LoadProfiles();
                        LoadLayoutComboBox();
                        LoadLayoutOptions();
                        LoadHighlighterSettings();
                        LoadGeneralSettings();

                        MessageBox.Show("Settings imported successfully. Some changes may require a restart.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to import settings. The file may be invalid.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import settings: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    #endregion
}
