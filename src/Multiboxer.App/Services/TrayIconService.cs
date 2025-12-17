using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Multiboxer.Core.Slots;

namespace Multiboxer.App.Services;

/// <summary>
/// Manages the system tray icon and context menu
/// </summary>
public class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly SlotManager _slotManager;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(SlotManager slotManager)
    {
        _slotManager = slotManager;
    }

    /// <summary>
    /// Initialize the tray icon
    /// </summary>
    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Multiboxer",
            Visibility = Visibility.Visible
        };

        // Try to load icon from resources, fallback to default
        try
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Use system icon as fallback
                _trayIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _trayIcon.Icon = SystemIcons.Application;
        }

        // Create context menu
        UpdateContextMenu();

        // Handle double-click to show window
        _trayIcon.TrayMouseDoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        // Subscribe to slot changes to update menu
        _slotManager.SlotAdded += (s, e) => UpdateContextMenu();
        _slotManager.SlotRemoved += (s, e) => UpdateContextMenu();
        _slotManager.ForegroundSlotChanged += (s, e) => UpdateContextMenu();
    }

    /// <summary>
    /// Update the context menu
    /// </summary>
    public void UpdateContextMenu()
    {
        if (_trayIcon == null)
            return;

        var menu = new ContextMenu();

        // Header
        var headerItem = new MenuItem
        {
            Header = "Multiboxer",
            IsEnabled = false,
            FontWeight = FontWeights.Bold
        };
        menu.Items.Add(headerItem);
        menu.Items.Add(new Separator());

        // Active Slots submenu
        var slotsMenu = new MenuItem { Header = "Active Slots" };
        var activeSlots = _slotManager.GetActiveSlots().ToList();

        if (activeSlots.Count == 0)
        {
            var noSlotsItem = new MenuItem
            {
                Header = "(No active slots)",
                IsEnabled = false
            };
            slotsMenu.Items.Add(noSlotsItem);
        }
        else
        {
            foreach (var slot in activeSlots)
            {
                var slotItem = new MenuItem
                {
                    Header = $"Slot {slot.Id}: {(string.IsNullOrEmpty(slot.WindowTitle) ? slot.ProfileName : slot.WindowTitle)}",
                    IsChecked = slot.State == SlotState.Foreground
                };

                var capturedSlot = slot;
                slotItem.Click += (s, e) =>
                {
                    _slotManager.FocusSlot(capturedSlot.Id);
                    App.LayoutEngine.ApplyLayoutWithMain(capturedSlot.Id);
                };

                slotsMenu.Items.Add(slotItem);
            }

            slotsMenu.Items.Add(new Separator());

            // Close All option
            var closeAllItem = new MenuItem { Header = "Close All" };
            closeAllItem.Click += (s, e) =>
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to close all slots?",
                    "Confirm Close All",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _slotManager.CloseAll();
                }
            };
            slotsMenu.Items.Add(closeAllItem);
        }

        menu.Items.Add(slotsMenu);

        // Layout submenu
        var layoutMenu = new MenuItem { Header = "Layout" };
        foreach (var layoutName in App.LayoutEngine.GetAvailableLayouts())
        {
            var layoutItem = new MenuItem
            {
                Header = layoutName,
                IsChecked = App.LayoutEngine.CurrentStrategy.Name == layoutName
            };

            var capturedName = layoutName;
            layoutItem.Click += (s, e) =>
            {
                App.LayoutEngine.SetStrategy(capturedName);
                App.LayoutEngine.ApplyLayout();
                UpdateContextMenu();
            };

            layoutMenu.Items.Add(layoutItem);
        }

        layoutMenu.Items.Add(new Separator());

        var applyLayoutItem = new MenuItem { Header = "Apply Layout Now" };
        applyLayoutItem.Click += (s, e) => App.LayoutEngine.ApplyLayout();
        layoutMenu.Items.Add(applyLayoutItem);

        menu.Items.Add(layoutMenu);

        menu.Items.Add(new Separator());

        // Show Window
        var showItem = new MenuItem { Header = "Show Window" };
        showItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showItem);

        menu.Items.Add(new Separator());

        // Exit
        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
    }

    /// <summary>
    /// Show a balloon notification
    /// </summary>
    public void ShowNotification(string title, string message, BalloonIcon icon = BalloonIcon.Info)
    {
        _trayIcon?.ShowBalloonTip(title, message, icon);
    }

    /// <summary>
    /// Update tooltip text
    /// </summary>
    public void UpdateTooltip(string text)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = text;
        }
    }

    /// <summary>
    /// Show or hide the tray icon
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _trayIcon?.Dispose();
        _trayIcon = null;

        GC.SuppressFinalize(this);
    }
}
