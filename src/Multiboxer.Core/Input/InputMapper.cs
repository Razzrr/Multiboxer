using System.Runtime.InteropServices;
using Multiboxer.Core.Logging;
using Multiboxer.Native;

namespace Multiboxer.Core.Input;

/// <summary>
/// Maps coordinates from preview thumbnails to actual game client coordinates.
/// Handles DPI scaling, window borders, and monitor differences.
/// </summary>
public class InputMapper
{
    private readonly Dictionary<int, SlotMapping> _mappings = new();
    private readonly object _lock = new();

    /// <summary>
    /// Mapping data for a single slot
    /// </summary>
    private class SlotMapping
    {
        public IntPtr WindowHandle { get; set; }
        public double DpiScale { get; set; } = 1.0;
        public RECT WindowRect { get; set; }
        public RECT ClientRect { get; set; }
        public int ClientOffsetX { get; set; }
        public int ClientOffsetY { get; set; }
        public int PreviewX { get; set; }
        public int PreviewY { get; set; }
        public int PreviewWidth { get; set; }
        public int PreviewHeight { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Refresh input mapping for a slot
    /// Call this when window handle changes, monitor changes, or after layout application
    /// </summary>
    public void RefreshMapping(int slotId, IntPtr hwnd, int previewX, int previewY, int previewWidth, int previewHeight)
    {
        if (hwnd == IntPtr.Zero)
            return;

        lock (_lock)
        {
            var mapping = GetOrCreateMapping(slotId);
            mapping.WindowHandle = hwnd;
            mapping.PreviewX = previewX;
            mapping.PreviewY = previewY;
            mapping.PreviewWidth = previewWidth;
            mapping.PreviewHeight = previewHeight;

            // Get DPI for the window
            mapping.DpiScale = GetDpiScaleForWindow(hwnd);

            // Get window rect (includes borders if any)
            if (User32.GetWindowRect(hwnd, out var windowRect))
            {
                mapping.WindowRect = windowRect;
            }

            // Get client rect (the actual rendering area)
            if (GetClientRect(hwnd, out var clientRect))
            {
                mapping.ClientRect = clientRect;

                // Calculate offset from window to client area
                // This accounts for title bars, borders, etc.
                var clientPoint = new POINT { X = 0, Y = 0 };
                if (ClientToScreen(hwnd, ref clientPoint))
                {
                    mapping.ClientOffsetX = clientPoint.X - mapping.WindowRect.Left;
                    mapping.ClientOffsetY = clientPoint.Y - mapping.WindowRect.Top;
                }
            }

            mapping.LastUpdate = DateTime.Now;

            DebugLog.InputMapping(slotId, mapping.DpiScale,
                mapping.ClientRect.Left, mapping.ClientRect.Top,
                mapping.ClientRect.Width, mapping.ClientRect.Height,
                mapping.WindowRect.Left, mapping.WindowRect.Top,
                mapping.WindowRect.Width, mapping.WindowRect.Height);

            // Validate by mapping center point
            ValidateMapping(slotId, mapping);
        }
    }

    /// <summary>
    /// Map a point from preview coordinates to client coordinates
    /// </summary>
    /// <param name="slotId">The slot ID</param>
    /// <param name="previewX">X coordinate relative to preview window</param>
    /// <param name="previewY">Y coordinate relative to preview window</param>
    /// <returns>Client coordinates, or null if mapping fails</returns>
    public (int X, int Y)? MapPreviewToClient(int slotId, double previewX, double previewY)
    {
        lock (_lock)
        {
            if (!_mappings.TryGetValue(slotId, out var mapping))
                return null;

            // Calculate scale factors
            // Preview shows the full window, so we need to map to client area
            double scaleX = (double)mapping.ClientRect.Width / mapping.PreviewWidth;
            double scaleY = (double)mapping.ClientRect.Height / mapping.PreviewHeight;

            // Account for DPI
            scaleX *= mapping.DpiScale;
            scaleY *= mapping.DpiScale;

            // Map the point
            int clientX = (int)(previewX * scaleX);
            int clientY = (int)(previewY * scaleY);

            // Validate bounds
            bool valid = clientX >= 0 && clientX < mapping.ClientRect.Width &&
                        clientY >= 0 && clientY < mapping.ClientRect.Height;

            DebugLog.InputMappedPoint(slotId, previewX, previewY, clientX, clientY, valid);

            if (!valid)
            {
                // Clamp to valid range
                clientX = Math.Clamp(clientX, 0, mapping.ClientRect.Width - 1);
                clientY = Math.Clamp(clientY, 0, mapping.ClientRect.Height - 1);
            }

            return (clientX, clientY);
        }
    }

    /// <summary>
    /// Map a point from preview coordinates to screen coordinates
    /// </summary>
    public (int X, int Y)? MapPreviewToScreen(int slotId, double previewX, double previewY)
    {
        lock (_lock)
        {
            if (!_mappings.TryGetValue(slotId, out var mapping))
                return null;

            var clientCoords = MapPreviewToClient(slotId, previewX, previewY);
            if (!clientCoords.HasValue)
                return null;

            // Convert client to screen
            var point = new POINT { X = clientCoords.Value.X, Y = clientCoords.Value.Y };
            if (ClientToScreen(mapping.WindowHandle, ref point))
            {
                return (point.X, point.Y);
            }

            return null;
        }
    }

    /// <summary>
    /// Get the current DPI scale for a slot
    /// </summary>
    public double GetDpiScale(int slotId)
    {
        lock (_lock)
        {
            if (_mappings.TryGetValue(slotId, out var mapping))
                return mapping.DpiScale;
            return 1.0;
        }
    }

    /// <summary>
    /// Check if mapping needs refresh (stale or missing)
    /// </summary>
    public bool NeedsRefresh(int slotId, IntPtr currentHwnd)
    {
        lock (_lock)
        {
            if (!_mappings.TryGetValue(slotId, out var mapping))
                return true;

            // Handle changed
            if (mapping.WindowHandle != currentHwnd)
                return true;

            // Stale (older than 5 seconds)
            if ((DateTime.Now - mapping.LastUpdate).TotalSeconds > 5)
                return true;

            return false;
        }
    }

    /// <summary>
    /// Remove mapping for a slot
    /// </summary>
    public void RemoveMapping(int slotId)
    {
        lock (_lock)
        {
            _mappings.Remove(slotId);
        }
    }

    /// <summary>
    /// Clear all mappings
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _mappings.Clear();
        }
    }

    private SlotMapping GetOrCreateMapping(int slotId)
    {
        if (!_mappings.TryGetValue(slotId, out var mapping))
        {
            mapping = new SlotMapping();
            _mappings[slotId] = mapping;
        }
        return mapping;
    }

    private void ValidateMapping(int slotId, SlotMapping mapping)
    {
        // Map center of preview to client
        double centerPreviewX = mapping.PreviewWidth / 2.0;
        double centerPreviewY = mapping.PreviewHeight / 2.0;

        var center = MapPreviewToClient(slotId, centerPreviewX, centerPreviewY);
        if (center.HasValue)
        {
            bool valid = center.Value.X > 0 && center.Value.X < mapping.ClientRect.Width &&
                        center.Value.Y > 0 && center.Value.Y < mapping.ClientRect.Height;

            if (!valid)
            {
                // Mapping seems wrong, try to recalculate
                System.Diagnostics.Debug.WriteLine($"InputMapper: Center point validation failed for slot {slotId}");
            }
        }
    }

    private static double GetDpiScaleForWindow(IntPtr hwnd)
    {
        try
        {
            // Try GetDpiForWindow (Windows 10 1607+)
            var dpi = User32.GetDpiForWindow(hwnd);
            if (dpi > 0)
            {
                return dpi / 96.0;
            }
        }
        catch
        {
            // Ignore
        }

        try
        {
            // Fallback: Get monitor DPI
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    return dpiX / 96.0;
                }
            }
        }
        catch
        {
            // Ignore
        }

        // Final fallback: assume 96 DPI (100%)
        return 1.0;
    }

    #region Native Methods

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion
}
