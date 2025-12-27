using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    /// Update the thumbnail display properties.
    /// Uses "Cover" scaling mode: scales source to fill destination completely,
    /// cropping the excess (no letterboxing/padding).
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

        // Avoid division by zero
        if (sourceSize.cx <= 0 || sourceSize.cy <= 0 || destWidth <= 0 || destHeight <= 0)
        {
            Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Invalid dimensions, skipping update");
            return;
        }

        // Calculate aspect ratios for Cover mode scaling
        double sourceAspect = (double)sourceSize.cx / sourceSize.cy;
        double destAspect = (double)destWidth / destHeight;

        // Cover mode: calculate source rectangle to crop for fill scaling
        // This eliminates letterboxing by cropping the source to match destination aspect ratio
        int srcLeft = 0, srcTop = 0, srcRight = sourceSize.cx, srcBottom = sourceSize.cy;

        if (sourceAspect > destAspect)
        {
            // Source is wider - crop left/right
            int visibleWidth = (int)(sourceSize.cy * destAspect);
            int offset = (sourceSize.cx - visibleWidth) / 2;
            srcLeft = offset;
            srcRight = sourceSize.cx - offset;
        }
        else if (sourceAspect < destAspect)
        {
            // Source is taller - crop top/bottom
            int visibleHeight = (int)(sourceSize.cx / destAspect);
            int offset = (sourceSize.cy - visibleHeight) / 2;
            srcTop = offset;
            srcBottom = sourceSize.cy - offset;
        }
        // If aspects match, use full source (no cropping needed)

        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Cover mode - dest={destWidth}x{destHeight}, source={sourceSize.cx}x{sourceSize.cy}, crop=({srcLeft},{srcTop})-({srcRight},{srcBottom})");

        // Set both source and destination rectangles for Cover scaling
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmThumbnailFlags.DWM_TNP_VISIBLE |
                      DwmThumbnailFlags.DWM_TNP_RECTDESTINATION |
                      DwmThumbnailFlags.DWM_TNP_RECTSOURCE |
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
            },
            rcSource = new RECT
            {
                Left = srcLeft,
                Top = srcTop,
                Right = srcRight,
                Bottom = srcBottom
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
    /// Force recreation of the thumbnail by clearing the handle.
    /// Call this when the source window transitions from foreground to background
    /// to prevent stale frame bleed-through.
    /// </summary>
    public void ForceRecreate()
    {
        var sourceBackup = _sourceWindow;
        UnregisterThumbnail();
        _sourceWindow = IntPtr.Zero;  // Force SetSource to re-register
        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: ForceRecreate - cleared thumbnail handle");
        if (sourceBackup != IntPtr.Zero)
        {
            SetSource(sourceBackup);
        }
    }

    /// <summary>
    /// Clear the surface to black to prevent stale frame bleed-through.
    /// </summary>
    public void ClearSurface()
    {
        ThumbnailBorder.Background = Brushes.Black;
        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: Surface cleared to black");
    }

    /// <summary>
    /// Restore the surface after clearing.
    /// </summary>
    public void RestoreSurface()
    {
        ThumbnailBorder.Background = Brushes.Transparent;
    }

    /// <summary>
    /// Position and size the thumbnail window (coordinates in physical pixels)
    /// </summary>
    public void SetPosition(int x, int y, int width, int height)
    {
        // WPF uses DIPs (device-independent pixels), not physical pixels
        // We need to convert physical pixels to DIPs using the DPI scale factor
        var dpiScale = GetDpiScale();

        Left = x / dpiScale;
        Top = y / dpiScale;
        Width = width / dpiScale;
        Height = height / dpiScale;

        Debug.WriteLine($"DwmThumbnailWindow slot {_slotId}: SetPosition pixels=({x},{y}) {width}x{height} -> DIPs=({Left:F0},{Top:F0}) {Width:F0}x{Height:F0} (scale={dpiScale:F2})");
    }

    /// <summary>
    /// Get the DPI scale factor for this window
    /// </summary>
    private double GetDpiScale()
    {
        // Try to get DPI from the window's presentation source
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformToDevice.M11;
        }

        // Fallback: use system DPI
        return System.Windows.SystemParameters.PrimaryScreenHeight /
               System.Windows.SystemParameters.WorkArea.Height;
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
