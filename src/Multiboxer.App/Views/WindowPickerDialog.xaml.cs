using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Multiboxer.Core.Window;

namespace Multiboxer.App.Views;

/// <summary>
/// Dialog for selecting existing windows to attach to slots
/// </summary>
public partial class WindowPickerDialog : Window
{
    private readonly ObservableCollection<WindowItem> _allWindows = new();
    private readonly ObservableCollection<WindowItem> _filteredWindows = new();

    /// <summary>
    /// Selected window (null if cancelled)
    /// </summary>
    public WindowItem? SelectedWindow { get; private set; }

    /// <summary>
    /// Selected slot ID
    /// </summary>
    public int SelectedSlotId { get; private set; }

    public WindowPickerDialog()
    {
        InitializeComponent();

        WindowsListView.ItemsSource = _filteredWindows;

        // Populate slot combo box
        var availableSlots = new List<string> { "Next Available" };
        for (int i = 1; i <= 40; i++)
        {
            var slot = App.SlotManager.GetSlot(i);
            if (slot == null || !slot.HasProcess)
            {
                availableSlots.Add($"Slot {i}");
            }
        }
        SlotComboBox.ItemsSource = availableSlots;
        SlotComboBox.SelectedIndex = 0;

        // Load windows
        RefreshWindowList();
    }

    private void RefreshWindowList()
    {
        _allWindows.Clear();

        var windows = WindowHelper.FindVisibleWindows();
        var currentProcessId = Process.GetCurrentProcess().Id;

        foreach (var hwnd in windows)
        {
            try
            {
                Native.User32.GetWindowThreadProcessId(hwnd, out var processId);

                // Skip our own windows
                if (processId == currentProcessId)
                    continue;

                // Skip windows already attached to slots
                if (App.SlotManager.ActiveSlots.Any(s => s.MainWindowHandle == hwnd))
                    continue;

                var title = WindowHelper.GetWindowTitle(hwnd);
                var className = WindowHelper.GetWindowClassName(hwnd);

                // Skip certain system windows
                if (string.IsNullOrWhiteSpace(title))
                    continue;
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd")
                    continue;

                string processName = "Unknown";
                try
                {
                    var process = Process.GetProcessById((int)processId);
                    processName = process.ProcessName;
                }
                catch { }

                _allWindows.Add(new WindowItem
                {
                    Handle = hwnd,
                    Title = title,
                    ClassName = className,
                    ProcessId = (int)processId,
                    ProcessName = processName
                });
            }
            catch
            {
                // Skip windows we can't access
            }
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterTextBox.Text.Trim();
        _filteredWindows.Clear();

        foreach (var window in _allWindows)
        {
            if (string.IsNullOrEmpty(filter) ||
                window.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                window.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                window.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _filteredWindows.Add(window);
            }
        }
    }

    private int GetSelectedSlotId()
    {
        if (SlotComboBox.SelectedIndex == 0)
        {
            // Next available
            return App.SlotManager.GetNextAvailableSlotId();
        }
        else
        {
            // Parse slot number from selection
            var selection = SlotComboBox.SelectedItem as string;
            if (selection != null && selection.StartsWith("Slot "))
            {
                if (int.TryParse(selection.Substring(5), out var slotId))
                {
                    return slotId;
                }
            }
            return App.SlotManager.GetNextAvailableSlotId();
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void WindowsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AttachButton.IsEnabled = WindowsListView.SelectedItem != null;
    }

    private void WindowsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (WindowsListView.SelectedItem is WindowItem window)
        {
            SelectedWindow = window;
            SelectedSlotId = GetSelectedSlotId();

            if (SelectedSlotId > 0)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("No available slots.", "Cannot Attach", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsListView.SelectedItem is WindowItem window)
        {
            SelectedWindow = window;
            SelectedSlotId = GetSelectedSlotId();

            if (SelectedSlotId > 0)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("No available slots.", "Cannot Attach", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Represents a window in the picker list
/// </summary>
public class WindowItem
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
}
