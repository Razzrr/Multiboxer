using Multiboxer.Native;

namespace Multiboxer.Core.Window;

/// <summary>
/// Information about a display monitor
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// Handle to the monitor
    /// </summary>
    public IntPtr Handle { get; init; }

    /// <summary>
    /// Device name of the monitor
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is the primary monitor
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Full monitor bounds (including taskbar area)
    /// </summary>
    public Rectangle Bounds { get; init; }

    /// <summary>
    /// Working area (excluding taskbar)
    /// </summary>
    public Rectangle WorkingArea { get; init; }

    /// <summary>
    /// Monitor index (0-based)
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Get the X coordinate of the monitor
    /// </summary>
    public int X => Bounds.X;

    /// <summary>
    /// Get the Y coordinate of the monitor
    /// </summary>
    public int Y => Bounds.Y;

    /// <summary>
    /// Get the width of the monitor
    /// </summary>
    public int Width => Bounds.Width;

    /// <summary>
    /// Get the height of the monitor
    /// </summary>
    public int Height => Bounds.Height;

    public override string ToString()
    {
        return $"{DeviceName} ({Width}x{Height}){(IsPrimary ? " [Primary]" : "")}";
    }
}

/// <summary>
/// Simple rectangle structure
/// </summary>
public readonly struct Rectangle
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public Rectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public static Rectangle FromRECT(RECT rect)
    {
        return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public bool Contains(int x, int y)
    {
        return x >= Left && x < Right && y >= Top && y < Bottom;
    }

    public bool Intersects(Rectangle other)
    {
        return Left < other.Right && Right > other.Left &&
               Top < other.Bottom && Bottom > other.Top;
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Width}x{Height})";
    }
}

/// <summary>
/// Manager for multi-monitor support
/// </summary>
public static class MonitorManager
{
    /// <summary>
    /// Get information about all monitors
    /// </summary>
    public static List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        bool EnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var info = MONITORINFOEX.Create();
            if (User32.GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo
                {
                    Handle = hMonitor,
                    DeviceName = info.szDevice,
                    IsPrimary = (info.dwFlags & WindowStyleConstants.MONITORINFOF_PRIMARY) != 0,
                    Bounds = Rectangle.FromRECT(info.rcMonitor),
                    WorkingArea = Rectangle.FromRECT(info.rcWork),
                    Index = index++
                });
            }
            return true;
        }

        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumCallback, IntPtr.Zero);

        return monitors;
    }

    /// <summary>
    /// Get the primary monitor
    /// </summary>
    public static MonitorInfo? GetPrimaryMonitor()
    {
        return GetAllMonitors().FirstOrDefault(m => m.IsPrimary);
    }

    /// <summary>
    /// Get the monitor containing a specific window
    /// </summary>
    public static MonitorInfo? GetMonitorForWindow(IntPtr hWnd)
    {
        var hMonitor = User32.MonitorFromWindow(hWnd, MonitorDefaultTo.MONITOR_DEFAULTTONEAREST);
        return GetMonitorFromHandle(hMonitor);
    }

    /// <summary>
    /// Get the monitor containing a specific point
    /// </summary>
    public static MonitorInfo? GetMonitorForPoint(int x, int y)
    {
        var point = new POINT(x, y);
        var hMonitor = User32.MonitorFromPoint(point, MonitorDefaultTo.MONITOR_DEFAULTTONEAREST);
        return GetMonitorFromHandle(hMonitor);
    }

    /// <summary>
    /// Get monitor info from a handle
    /// </summary>
    private static MonitorInfo? GetMonitorFromHandle(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero)
            return null;

        var info = MONITORINFOEX.Create();
        if (!User32.GetMonitorInfo(hMonitor, ref info))
            return null;

        // Find the index
        var allMonitors = GetAllMonitors();
        var index = allMonitors.FindIndex(m => m.Handle == hMonitor);

        return new MonitorInfo
        {
            Handle = hMonitor,
            DeviceName = info.szDevice,
            IsPrimary = (info.dwFlags & WindowStyleConstants.MONITORINFOF_PRIMARY) != 0,
            Bounds = Rectangle.FromRECT(info.rcMonitor),
            WorkingArea = Rectangle.FromRECT(info.rcWork),
            Index = index >= 0 ? index : 0
        };
    }

    /// <summary>
    /// Get total virtual screen bounds (all monitors combined)
    /// </summary>
    public static Rectangle GetVirtualScreenBounds()
    {
        var monitors = GetAllMonitors();
        if (monitors.Count == 0)
        {
            return new Rectangle(0, 0,
                User32.GetSystemMetrics(User32.SM_CXSCREEN),
                User32.GetSystemMetrics(User32.SM_CYSCREEN));
        }

        int left = monitors.Min(m => m.Bounds.Left);
        int top = monitors.Min(m => m.Bounds.Top);
        int right = monitors.Max(m => m.Bounds.Right);
        int bottom = monitors.Max(m => m.Bounds.Bottom);

        return new Rectangle(left, top, right - left, bottom - top);
    }
}
