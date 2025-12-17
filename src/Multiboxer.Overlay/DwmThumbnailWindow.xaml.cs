using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Multiboxer.Native;

namespace Multiboxer.Overlay;

/// <summary>
/// Event args for thumbnail click events
/// </summary>
public class ThumbnailClickEventArgs : EventArgs
{
    public int SlotId { get; }

    public ThumbnailClickEventArgs(int slotId)
    {
        SlotId = slotId;
    }
}

/// <summary>
/// A window that displays a live DWM thumbnail of another window.
/// This is the "Video FX" approach - shows a scaled live view without resizing the source.
/// </summary>
public partial class DwmThumbnailWindow : Window
{
    private IntPtr _thumbnailHandle;
    private IntPtr _sourceWindow;
    private IntPtr _thisWindowHandle;
    private bool _isClosed;
    private int _slotId;

    /// <summary>
    /// Event raised when the thumbnail is clicked
    /// </summary>
    public event EventHandler<ThumbnailClickEventArgs>? ThumbnailClicked;

    public DwmThumbnailWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        SizeChanged += OnSizeChanged;
    }

    public int SlotId
    {
        get => _slotId;
        set
        {
            _slotId = value;
            SlotLabel.Text = $"Slot {_slotId}";
        }
    }

    public bool ShowSlotLabel
    {
        get => SlotLabel.Visibility == Visibility.Visible;
        set => SlotLabel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool ShowBorder
    {
        get => ThumbnailBorder.BorderThickness.Left > 0;
        set => ThumbnailBorder.BorderThickness = value ? new Thickness(2) : new Thickness(0);
    }

    public bool IsClosed => _isClosed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureHandle();
    }

    /// <summary>
    /// Ensure the window handle is created
    /// </summary>
    public void EnsureHandle()
    {
        if (_thisWindowHandle == IntPtr.Zero)
        {
            var helper = new WindowInteropHelper(this);
            // EnsureHandle forces the handle to be created immediately
            _thisWindowHandle = helper.EnsureHandle();
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Handle created = {_thisWindowHandle}");
            ApplyNoActivateStyles();
        }
    }

    /// <summary>
    /// Apply WS_EX_NOACTIVATE and WS_EX_TOOLWINDOW so the thumbnail never steals focus
    /// </summary>
    private void ApplyNoActivateStyles()
    {
        if (_thisWindowHandle == IntPtr.Zero)
            return;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        var extStyle = User32.GetWindowLongPtrSafe(_thisWindowHandle, GWL_EXSTYLE);
        extStyle = (IntPtr)((long)extStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        User32.SetWindowLongPtrSafe(_thisWindowHandle, GWL_EXSTYLE, extStyle);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isClosed = true;
        UnregisterThumbnail();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbnailProperties();
    }

    /// <summary>
    /// Set the source window to display a thumbnail of
    /// </summary>
    public bool SetSource(IntPtr sourceWindowHandle)
    {
        // Ensure we have a window handle
        EnsureHandle();

        if (_thisWindowHandle == IntPtr.Zero)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Failed to get window handle");
            return false;
        }

        // Unregister existing thumbnail
        if (_thumbnailHandle != IntPtr.Zero && _sourceWindow == sourceWindowHandle)
        {
            // Source unchanged; just refresh properties
            UpdateThumbnailProperties();
            return true;
        }

        UnregisterThumbnail();

        _sourceWindow = sourceWindowHandle;

        if (_sourceWindow == IntPtr.Zero)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Source window handle is zero");
            return false;
        }

        // Register new thumbnail
        int hr = Dwmapi.DwmRegisterThumbnail(_thisWindowHandle, _sourceWindow, out _thumbnailHandle);
        if (hr != 0)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: DwmRegisterThumbnail failed with hr={hr}");
            _thumbnailHandle = IntPtr.Zero;
            return false;
        }

        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Thumbnail registered successfully, handle={_thumbnailHandle}");

        // Query source size to verify it's valid
        Dwmapi.DwmQueryThumbnailSourceSize(_thumbnailHandle, out SIZE sourceSize);
        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Source size = {sourceSize.cx}x{sourceSize.cy}");

        UpdateThumbnailProperties();
        return true;
    }

    /// <summary>
    /// Update the thumbnail display properties
    /// </summary>
    public void UpdateThumbnailProperties()
    {
        if (_thumbnailHandle == IntPtr.Zero)
            return;

        // Get source window size
        Dwmapi.DwmQueryThumbnailSourceSize(_thumbnailHandle, out SIZE sourceSize);

        int destWidth = (int)ActualWidth;
        int destHeight = (int)ActualHeight;

        // If window size is 0, use the set Width/Height
        if (destWidth <= 0) destWidth = (int)Width;
        if (destHeight <= 0) destHeight = (int)Height;

        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: UpdateProps dest={destWidth}x{destHeight}, source={sourceSize.cx}x{sourceSize.cy}");

        // Calculate destination rectangle (fill the window)
        // DWM automatically scales the source to fit the destination
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmThumbnailFlags.DWM_TNP_VISIBLE |
                      DwmThumbnailFlags.DWM_TNP_RECTDESTINATION |
                      DwmThumbnailFlags.DWM_TNP_OPACITY,
            fVisible = true,
            fSourceClientAreaOnly = false,  // Capture entire window, not just client area
            opacity = 255,  // Full opacity
            rcDestination = new RECT
            {
                Left = 0,
                Top = 0,
                Right = destWidth,
                Bottom = destHeight
            }
        };

        int hr = Dwmapi.DwmUpdateThumbnailProperties(_thumbnailHandle, ref props);
        if (hr != 0)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: DwmUpdateThumbnailProperties failed hr={hr}");
        }
    }

    /// <summary>
    /// Unregister the current thumbnail
    /// </summary>
    private void UnregisterThumbnail()
    {
        if (_thumbnailHandle != IntPtr.Zero)
        {
            Dwmapi.DwmUnregisterThumbnail(_thumbnailHandle);
            _thumbnailHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Position and size the thumbnail window
    /// </summary>
    public void SetPosition(int x, int y, int width, int height)
    {
        Left = x;
        Top = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Make the window click-through or interactive
    /// </summary>
    public void MakeClickThrough(bool clickThrough)
    {
        if (_thisWindowHandle == IntPtr.Zero)
            return;

        var extStyle = User32.GetWindowLongPtrSafe(_thisWindowHandle, -20); // GWL_EXSTYLE
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_LAYERED = 0x00080000;

        if (clickThrough)
        {
            extStyle = (IntPtr)((long)extStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }
        else
        {
            extStyle = (IntPtr)((long)extStyle & ~WS_EX_TRANSPARENT);
        }

        User32.SetWindowLongPtrSafe(_thisWindowHandle, -20, extStyle);
    }

    /// <summary>
    /// Handle click on thumbnail to focus the source window
    /// </summary>
    public void FocusSourceWindow()
    {
        if (_sourceWindow != IntPtr.Zero)
        {
            User32.SetForegroundWindow(_sourceWindow);
        }
    }

    /// <summary>
    /// Handle mouse click on the thumbnail window
    /// </summary>
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Clicked!");
            ThumbnailClicked?.Invoke(this, new ThumbnailClickEventArgs(_slotId));
        }
    }
}
