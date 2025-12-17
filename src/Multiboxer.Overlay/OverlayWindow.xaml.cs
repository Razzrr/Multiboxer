using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Multiboxer.Core.Slots;
using Multiboxer.Native;

namespace Multiboxer.Overlay;

/// <summary>
/// Transparent overlay window that displays highlighter elements
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly Slot _slot;
    private readonly DispatcherTimer _updateTimer;
    private IntPtr _targetWindow;
    private bool _showBorder = true;
    private bool _showNumber = true;
    private bool _isForeground;
    private bool _isClosed;

    /// <summary>
    /// Whether to show the border highlight
    /// </summary>
    public bool ShowBorder
    {
        get => _showBorder;
        set
        {
            _showBorder = value;
            UpdateVisibility();
        }
    }

    /// <summary>
    /// Whether to show the slot number
    /// </summary>
    public bool ShowNumber
    {
        get => _showNumber;
        set
        {
            _showNumber = value;
            UpdateVisibility();
        }
    }

    /// <summary>
    /// Border color for the highlight
    /// </summary>
    public Color BorderColor
    {
        get => ((SolidColorBrush)HighlightBorder.BorderBrush).Color;
        set => HighlightBorder.BorderBrush = new SolidColorBrush(value);
    }

    /// <summary>
    /// Border thickness
    /// </summary>
    public double HighlightBorderThickness
    {
        get => HighlightBorder.BorderThickness.Left;
        set => HighlightBorder.BorderThickness = new Thickness(value);
    }

    public OverlayWindow(Slot slot)
    {
        InitializeComponent();

        _slot = slot;
        _targetWindow = slot.MainWindowHandle;

        SlotNumberText.Text = slot.Id.ToString();

        // Timer to track target window position
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OverlayWindow_Loaded;
        Closing += OverlayWindow_Closing;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Make the window click-through
        MakeClickThrough();

        // Start tracking
        _updateTimer.Start();
        UpdatePosition();
    }

    private void OverlayWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isClosed = true;
        _updateTimer.Stop();
    }

    /// <summary>
    /// Whether this window has been closed
    /// </summary>
    public bool IsClosed => _isClosed;

    /// <summary>
    /// Update the target window handle
    /// </summary>
    public void SetTargetWindow(IntPtr hwnd)
    {
        _targetWindow = hwnd;
        UpdatePosition();
    }

    /// <summary>
    /// Make this window click-through (WS_EX_TRANSPARENT)
    /// </summary>
    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = User32.GetWindowLongPtrSafe(hwnd, WindowStyleConstants.GWL_EXSTYLE);
        User32.SetWindowLongPtrSafe(hwnd, WindowStyleConstants.GWL_EXSTYLE,
            (IntPtr)((long)extendedStyle | (long)WindowStylesEx.WS_EX_TRANSPARENT | (long)WindowStylesEx.WS_EX_TOOLWINDOW));
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePosition();
        UpdateForegroundState();
    }

    /// <summary>
    /// Update overlay position to match target window
    /// </summary>
    private void UpdatePosition()
    {
        if (_targetWindow == IntPtr.Zero)
        {
            Hide();
            return;
        }

        // Check if target window still exists and is visible
        if (!User32.IsWindowVisible(_targetWindow))
        {
            Hide();
            return;
        }

        // Get target window position
        if (!User32.GetWindowRect(_targetWindow, out var rect))
        {
            Hide();
            return;
        }

        // Update overlay position
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;

        if (!IsVisible && !_isClosed)
        {
            Show();
        }
    }

    /// <summary>
    /// Update foreground state and visibility
    /// </summary>
    private void UpdateForegroundState()
    {
        var foregroundWindow = User32.GetForegroundWindow();
        var wasForeground = _isForeground;
        _isForeground = foregroundWindow == _targetWindow;

        if (wasForeground != _isForeground)
        {
            UpdateVisibility();
        }
    }

    /// <summary>
    /// Update element visibility based on settings and foreground state
    /// </summary>
    private void UpdateVisibility()
    {
        if (_isForeground)
        {
            // Foreground: show border, hide number
            HighlightBorder.Visibility = _showBorder ? Visibility.Visible : Visibility.Hidden;
            SlotNumberBorder.Visibility = Visibility.Hidden;
        }
        else
        {
            // Background: hide border, show number
            HighlightBorder.Visibility = Visibility.Hidden;
            SlotNumberBorder.Visibility = _showNumber ? Visibility.Visible : Visibility.Hidden;
        }
    }

    /// <summary>
    /// Force refresh the overlay
    /// </summary>
    public void Refresh()
    {
        _targetWindow = _slot.MainWindowHandle;
        SlotNumberText.Text = _slot.Id.ToString();
        UpdatePosition();
        UpdateForegroundState();
    }
}
