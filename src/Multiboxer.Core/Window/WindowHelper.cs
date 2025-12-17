using Multiboxer.Native;
using System.Text;

namespace Multiboxer.Core.Window;

/// <summary>
/// Helper methods for window manipulation
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// Force a window to the foreground, working around Windows restrictions
    /// </summary>
    public static bool ForceForegroundWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        var foregroundWindow = User32.GetForegroundWindow();

        // If already foreground, nothing to do
        if (foregroundWindow == hWnd)
            return true;

        // Get thread IDs
        var foregroundThreadId = User32.GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThreadId = User32.GetWindowThreadProcessId(hWnd, out var targetProcessId);
        var currentThreadId = User32.GetCurrentThreadId();

        bool attached = false;

        try
        {
            // Attach to the foreground thread if different
            if (foregroundThreadId != currentThreadId)
            {
                attached = User32.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // Allow our process to set the foreground window
            User32.AllowSetForegroundWindow((int)targetProcessId);

            // Try to bring window to foreground
            User32.BringWindowToTop(hWnd);

            // If minimized, restore it
            if (User32.IsIconic(hWnd))
            {
                User32.ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);
            }
            else
            {
                User32.ShowWindow(hWnd, ShowWindowCommand.SW_SHOW);
            }

            // Set foreground
            User32.SetForegroundWindow(hWnd);

            // Double-check with SetFocus
            User32.SetFocus(hWnd);

            return User32.GetForegroundWindow() == hWnd;
        }
        finally
        {
            if (attached)
            {
                User32.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// Check if a window is already borderless (no caption/border/thick frame).
    /// </summary>
    public static bool IsBorderlessApplied(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        var style = (WindowStyles)(long)User32.GetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE);
        return (style & (WindowStyles.WS_CAPTION | WindowStyles.WS_BORDER | WindowStyles.WS_THICKFRAME | WindowStyles.WS_DLGFRAME | WindowStyles.WS_SYSMENU)) == 0;
    }

    /// <summary>
    /// Remove window borders and title bar (make borderless)
    /// Like JMB's "-frame none" option
    /// </summary>
    public static bool MakeBorderless(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        try
        {
            // Get current styles
            var styleBefore = (WindowStyles)(long)User32.GetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE);
            var exStyleBefore = (WindowStylesEx)(long)User32.GetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_EXSTYLE);

            Debug.WriteLine($"MakeBorderless: hwnd=0x{hWnd:X}");
            Debug.WriteLine($"  Style BEFORE: 0x{(long)styleBefore:X8} (Caption={(styleBefore & WindowStyles.WS_CAPTION) != 0}, Border={(styleBefore & WindowStyles.WS_BORDER) != 0}, ThickFrame={(styleBefore & WindowStyles.WS_THICKFRAME) != 0})");

            // Remove window decorations (standard style)
            var style = styleBefore;
            style &= ~WindowStyles.WS_CAPTION;      // Title bar
            style &= ~WindowStyles.WS_BORDER;       // Thin border
            style &= ~WindowStyles.WS_THICKFRAME;   // Resizable border
            style &= ~WindowStyles.WS_DLGFRAME;     // Dialog frame
            style &= ~WindowStyles.WS_SYSMENU;      // System menu

            // Remove extended styles that add borders
            var exStyle = exStyleBefore;
            exStyle &= ~WindowStylesEx.WS_EX_DLGMODALFRAME;
            exStyle &= ~WindowStylesEx.WS_EX_CLIENTEDGE;
            exStyle &= ~WindowStylesEx.WS_EX_STATICEDGE;
            exStyle &= ~WindowStylesEx.WS_EX_WINDOWEDGE;

            // Apply both standard and extended styles
            var result1 = User32.SetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE, (IntPtr)(long)style);
            var result2 = User32.SetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_EXSTYLE, (IntPtr)(long)exStyle);

            // Force window to recalculate its frame
            var posResult = User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SetWindowPosFlags.SWP_NOMOVE |
                SetWindowPosFlags.SWP_NOSIZE |
                SetWindowPosFlags.SWP_NOZORDER |
                SetWindowPosFlags.SWP_NOACTIVATE |
                SetWindowPosFlags.SWP_FRAMECHANGED);

            // Verify the change took effect
            var styleAfter = (WindowStyles)(long)User32.GetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE);
            Debug.WriteLine($"  Style AFTER:  0x{(long)styleAfter:X8} (Caption={(styleAfter & WindowStyles.WS_CAPTION) != 0}, Border={(styleAfter & WindowStyles.WS_BORDER) != 0}, ThickFrame={(styleAfter & WindowStyles.WS_THICKFRAME) != 0})");
            Debug.WriteLine($"  SetWindowLongPtr returned: style=0x{result1:X}, exStyle=0x{result2:X}, SetWindowPos={posResult}");

            bool success = (styleAfter & WindowStyles.WS_CAPTION) == 0;
            if (!success)
            {
                Debug.WriteLine($"  WARNING: Style change did not stick! Window may be protected.");
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MakeBorderless EXCEPTION: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restore window borders and title bar
    /// </summary>
    public static void RestoreBorders(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return;

        var style = (WindowStyles)(long)User32.GetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE);

        // Add caption, border, and thick frame
        style |= WindowStyles.WS_CAPTION;
        style |= WindowStyles.WS_BORDER;
        style |= WindowStyles.WS_THICKFRAME;

        User32.SetWindowLongPtrSafe(hWnd, WindowStyleConstants.GWL_STYLE, (IntPtr)(long)style);

        // Force window to update
        User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SetWindowPosFlags.SWP_NOMOVE |
            SetWindowPosFlags.SWP_NOSIZE |
            SetWindowPosFlags.SWP_NOZORDER |
            SetWindowPosFlags.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Set window position and size
    /// </summary>
    public static bool SetWindowPosition(IntPtr hWnd, int x, int y, int width, int height, bool activate = false)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        var flags = SetWindowPosFlags.SWP_NOZORDER;
        if (!activate)
            flags |= SetWindowPosFlags.SWP_NOACTIVATE;

        return User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
    }

    /// <summary>
    /// Set window position and size with smooth resize to avoid DirectX reset issues
    /// This is particularly helpful for games like EverQuest that can error on rapid resizes
    /// </summary>
    public static async Task SetWindowPositionSmoothAsync(IntPtr hWnd, int x, int y, int width, int height, bool activate = false)
    {
        if (hWnd == IntPtr.Zero)
            return;

        // Get current position
        var (currentX, currentY, currentWidth, currentHeight) = GetWindowPosition(hWnd);

        // If size is changing significantly, do it in steps to avoid DirectX reset errors
        bool sizeChanging = Math.Abs(currentWidth - width) > 50 || Math.Abs(currentHeight - height) > 50;

        if (sizeChanging)
        {
            // First, move to the new position but keep the old size
            SetWindowPosition(hWnd, x, y, currentWidth, currentHeight, activate);
            await Task.Delay(50);

            // Then resize in one step
            SetWindowPosition(hWnd, x, y, width, height, false);
        }
        else
        {
            // Small change, do it directly
            SetWindowPosition(hWnd, x, y, width, height, activate);
        }
    }

    /// <summary>
    /// Set window position with deferred resize to help games handle resolution changes
    /// </summary>
    public static void SetWindowPositionDeferred(IntPtr hWnd, int x, int y, int width, int height, bool activate = false)
    {
        if (hWnd == IntPtr.Zero)
            return;

        // Use flags that minimize visual artifacts and DirectX reset issues
        var flags = SetWindowPosFlags.SWP_NOZORDER |
                    SetWindowPosFlags.SWP_ASYNCWINDOWPOS |
                    SetWindowPosFlags.SWP_NOCOPYBITS;  // Don't copy client area - helps with DirectX
        if (!activate)
            flags |= SetWindowPosFlags.SWP_NOACTIVATE;

        // Use async window positioning - less likely to cause DirectX issues
        User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
    }

    /// <summary>
    /// Batch reposition multiple windows at once using DeferWindowPos
    /// This is the smoothest way to move multiple windows - similar to JMB's approach
    /// </summary>
    public static void SetWindowPositionsBatched(IReadOnlyList<(IntPtr hWnd, int x, int y, int width, int height)> windowPositions)
    {
        Debug.WriteLine($"SetWindowPositionsBatched: {windowPositions.Count} windows");

        if (windowPositions.Count == 0)
            return;

        // Use DeferWindowPos for batched atomic updates
        var hdwp = User32.BeginDeferWindowPos(windowPositions.Count);
        if (hdwp == IntPtr.Zero)
        {
            Debug.WriteLine($"  BeginDeferWindowPos FAILED, using fallback");
            // Fallback to individual positioning
            foreach (var (hWnd, x, y, width, height) in windowPositions)
            {
                SetWindowPosition(hWnd, x, y, width, height);
            }
            return;
        }

        try
        {
            foreach (var (hWnd, x, y, width, height) in windowPositions)
            {
                if (hWnd == IntPtr.Zero)
                    continue;

                // Use minimal flags - DirectX games need to receive window messages
                var flags = SetWindowPosFlags.SWP_NOZORDER |
                            SetWindowPosFlags.SWP_NOACTIVATE;

                Debug.WriteLine($"  DeferWindowPos: hwnd=0x{hWnd:X} -> ({x},{y}) {width}x{height}");
                hdwp = User32.DeferWindowPos(hdwp, hWnd, IntPtr.Zero, x, y, width, height, flags);
                if (hdwp == IntPtr.Zero)
                {
                    Debug.WriteLine($"    DeferWindowPos FAILED for hwnd=0x{hWnd:X}, using fallback");
                    // DeferWindowPos failed, fallback to direct positioning
                    SetWindowPosition(hWnd, x, y, width, height);
                }
            }
        }
        finally
        {
            if (hdwp != IntPtr.Zero)
            {
                var result = User32.EndDeferWindowPos(hdwp);
                Debug.WriteLine($"  EndDeferWindowPos result: {result}");

                // Verify actual positions after applying
                Debug.WriteLine($"  Verifying actual window positions:");
                foreach (var (hWnd, x, y, width, height) in windowPositions)
                {
                    if (hWnd == IntPtr.Zero) continue;
                    var actual = GetWindowPosition(hWnd);
                    bool posMatch = (actual.X == x && actual.Y == y);
                    bool sizeMatch = (actual.Width == width && actual.Height == height);
                    Debug.WriteLine($"    hwnd=0x{hWnd:X}: expected ({x},{y}) {width}x{height}, actual ({actual.X},{actual.Y}) {actual.Width}x{actual.Height} - pos:{(posMatch ? "OK" : "MISMATCH")} size:{(sizeMatch ? "OK" : "MISMATCH")}");
                }
            }
        }
    }

    /// <summary>
    /// Get the desktop window handle
    /// </summary>
    public static IntPtr GetDesktopWindow()
    {
        return User32.GetDesktopWindow();
    }

    /// <summary>
    /// Get window position and size
    /// </summary>
    public static (int X, int Y, int Width, int Height) GetWindowPosition(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return (0, 0, 0, 0);

        if (User32.GetWindowRect(hWnd, out var rect))
        {
            return (rect.Left, rect.Top, rect.Width, rect.Height);
        }

        return (0, 0, 0, 0);
    }

    /// <summary>
    /// Get window title text
    /// </summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        var length = User32.GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var sb = new StringBuilder(length + 1);
        User32.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Get DPI scale for a window (returns 1.0 if unavailable)
    /// </summary>
    public static double GetDpiScale(IntPtr hWnd)
    {
        try
        {
            if (hWnd != IntPtr.Zero)
            {
                var dpi = User32.GetDpiForWindow(hWnd);
                if (dpi > 0)
                {
                    return dpi / 96.0;
                }
            }
        }
        catch
        {
            // Ignore; fall through to 1.0
        }
        return 1.0;
    }

    /// <summary>
    /// Set window title text
    /// </summary>
    public static bool SetWindowTitle(IntPtr hWnd, string title)
    {
        if (hWnd == IntPtr.Zero)
            return false;
        return User32.SetWindowText(hWnd, title);
    }

    /// <summary>
    /// Get window class name
    /// </summary>
    public static string GetWindowClassName(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return string.Empty;

        var sb = new StringBuilder(256);
        User32.GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Check if window is visible
    /// </summary>
    public static bool IsWindowVisible(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && User32.IsWindowVisible(hWnd);
    }

    /// <summary>
    /// Check if window is minimized
    /// </summary>
    public static bool IsWindowMinimized(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && User32.IsIconic(hWnd);
    }

    /// <summary>
    /// Check if window is maximized
    /// </summary>
    public static bool IsWindowMaximized(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && User32.IsZoomed(hWnd);
    }

    /// <summary>
    /// Minimize a window
    /// </summary>
    public static void MinimizeWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
            User32.ShowWindow(hWnd, ShowWindowCommand.SW_MINIMIZE);
    }

    /// <summary>
    /// Maximize a window
    /// </summary>
    public static void MaximizeWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
            User32.ShowWindow(hWnd, ShowWindowCommand.SW_SHOWMAXIMIZED);
    }

    /// <summary>
    /// Restore a window from minimized/maximized state
    /// </summary>
    public static void RestoreWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
            User32.ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);
    }

    /// <summary>
    /// Find all top-level windows matching criteria
    /// </summary>
    public static List<IntPtr> FindWindows(Func<IntPtr, bool>? predicate = null)
    {
        var windows = new List<IntPtr>();

        User32.EnumWindows((hWnd, lParam) =>
        {
            if (predicate == null || predicate(hWnd))
            {
                windows.Add(hWnd);
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Find windows by title (partial match)
    /// </summary>
    public static List<IntPtr> FindWindowsByTitle(string titlePart)
    {
        return FindWindows(hWnd =>
        {
            var title = GetWindowTitle(hWnd);
            return !string.IsNullOrEmpty(title) &&
                   title.Contains(titlePart, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Find windows by class name
    /// </summary>
    public static List<IntPtr> FindWindowsByClassName(string className)
    {
        return FindWindows(hWnd =>
        {
            var windowClass = GetWindowClassName(hWnd);
            return windowClass.Equals(className, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Find all visible top-level windows with titles
    /// </summary>
    public static List<IntPtr> FindVisibleWindows()
    {
        return FindWindows(hWnd =>
            IsWindowVisible(hWnd) &&
            !string.IsNullOrEmpty(GetWindowTitle(hWnd)));
    }
}
