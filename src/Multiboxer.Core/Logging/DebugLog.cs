using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Multiboxer.Core.Logging;

/// <summary>
/// Centralized debug logging for multiboxer operations.
/// Provides detailed event logging for swap, layout, thumbnail, and input operations.
/// </summary>
public static class DebugLog
{
    private static readonly string LogPath;
    private static readonly object _fileLock = new();
    private static StreamWriter? _writer;
    private static readonly ConcurrentQueue<string> _buffer = new();
    private static readonly System.Timers.Timer _flushTimer;
    private static bool _initialized;

    static DebugLog()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appData, "Multiboxer");
        Directory.CreateDirectory(logDir);
        LogPath = Path.Combine(logDir, "multiboxer_debug.log");

        _flushTimer = new System.Timers.Timer(500);
        _flushTimer.Elapsed += (s, e) => FlushBuffer();
        _flushTimer.AutoReset = true;
    }

    /// <summary>
    /// Initialize the debug log system
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Rotate old log if it exists and is large
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 10 * 1024 * 1024)
            {
                var backupPath = LogPath + ".old";
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(LogPath, backupPath);
            }

            _writer = new StreamWriter(LogPath, append: true, Encoding.UTF8)
            {
                AutoFlush = false
            };

            _flushTimer.Start();
            _initialized = true;

            Log("INIT", "========== Multiboxer Debug Log Started ==========");
            Log("INIT", $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            Log("INIT", $"Log Path: {LogPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize debug log: {ex.Message}");
        }
    }

    /// <summary>
    /// Shutdown the debug log system
    /// </summary>
    public static void Shutdown()
    {
        if (!_initialized) return;

        Log("SHUTDOWN", "========== Multiboxer Debug Log Ended ==========");
        FlushBuffer();

        _flushTimer.Stop();
        _writer?.Dispose();
        _writer = null;
        _initialized = false;
    }

    private static void Log(string category, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{category}] {message}";

        _buffer.Enqueue(line);
        Debug.WriteLine(line);
    }

    private static void FlushBuffer()
    {
        if (_writer == null) return;

        lock (_fileLock)
        {
            while (_buffer.TryDequeue(out var line))
            {
                _writer.WriteLine(line);
            }
            _writer.Flush();
        }
    }

    #region Hotkey Events

    /// <summary>
    /// Log hotkey press event
    /// </summary>
    public static void HotkeyPressed(int slotId, string currentState, DateTime timestamp)
    {
        Log("HOTKEY", $"Pressed: slot={slotId}, state={currentState}, time={timestamp:HH:mm:ss.fff}");
    }

    /// <summary>
    /// Log hotkey ignored (slot not active)
    /// </summary>
    public static void HotkeyIgnored(int slotId, string reason)
    {
        Log("HOTKEY", $"Ignored: slot={slotId}, reason={reason}");
    }

    #endregion

    #region Swap Queue Events

    /// <summary>
    /// Log swap queue decision
    /// </summary>
    public static void SwapQueueDecision(int requestedSlot, int? queuedSlot, string decision)
    {
        Log("QUEUE", $"Decision: requested={requestedSlot}, queued={queuedSlot?.ToString() ?? "none"}, action={decision}");
    }

    /// <summary>
    /// Log swap coalesced (multiple requests merged)
    /// </summary>
    public static void SwapCoalesced(int oldTarget, int newTarget, int droppedCount)
    {
        Log("QUEUE", $"Coalesced: {oldTarget} -> {newTarget}, dropped={droppedCount} intermediate requests");
    }

    /// <summary>
    /// Log swap debounced
    /// </summary>
    public static void SwapDebounced(int slotId, double msRemaining)
    {
        Log("QUEUE", $"Debounced: slot={slotId}, waiting={msRemaining:F0}ms");
    }

    #endregion

    #region State Transitions

    /// <summary>
    /// Log state machine transition
    /// </summary>
    public static void StateTransition(string oldState, string newState, string reason)
    {
        Log("STATE", $"Transition: {oldState} -> {newState}, reason={reason}");
    }

    /// <summary>
    /// Log state machine blocked
    /// </summary>
    public static void StateBlocked(string currentState, string attemptedAction, string reason)
    {
        Log("STATE", $"Blocked: state={currentState}, action={attemptedAction}, reason={reason}");
    }

    #endregion

    #region Window Metrics

    /// <summary>
    /// Log window metrics before/after layout
    /// </summary>
    public static void WindowMetrics(int slotId, IntPtr hwnd, string phase,
        int x, int y, int width, int height,
        int monitorIndex, double dpi, uint style, string zOrder)
    {
        Log("WINDOW", $"Metrics [{phase}]: slot={slotId}, hwnd=0x{hwnd:X}, " +
            $"rect=({x},{y}) {width}x{height}, monitor={monitorIndex}, dpi={dpi:F2}, " +
            $"style=0x{style:X8}, z={zOrder}");
    }

    /// <summary>
    /// Log window position verification
    /// </summary>
    public static void WindowVerification(int slotId, IntPtr hwnd,
        int expectedX, int expectedY, int expectedW, int expectedH,
        int actualX, int actualY, int actualW, int actualH,
        bool posMatch, bool sizeMatch)
    {
        var status = (posMatch && sizeMatch) ? "OK" : "MISMATCH";
        Log("WINDOW", $"Verify [{status}]: slot={slotId}, hwnd=0x{hwnd:X}, " +
            $"expected=({expectedX},{expectedY}) {expectedW}x{expectedH}, " +
            $"actual=({actualX},{actualY}) {actualW}x{actualH}");
    }

    /// <summary>
    /// Log borderless application
    /// </summary>
    public static void BorderlessApplied(int slotId, IntPtr hwnd, bool success, bool wasAlreadyApplied)
    {
        if (wasAlreadyApplied)
            Log("WINDOW", $"Borderless: slot={slotId}, hwnd=0x{hwnd:X}, skipped (already applied)");
        else
            Log("WINDOW", $"Borderless: slot={slotId}, hwnd=0x{hwnd:X}, result={success}");
    }

    #endregion

    #region Thumbnail Events

    /// <summary>
    /// Log thumbnail creation
    /// </summary>
    public static void ThumbnailCreated(int slotId, IntPtr sourceHwnd, IntPtr thumbHandle,
        int x, int y, int width, int height)
    {
        Log("THUMB", $"Created: slot={slotId}, source=0x{sourceHwnd:X}, handle=0x{thumbHandle:X}, " +
            $"rect=({x},{y}) {width}x{height}");
    }

    /// <summary>
    /// Log thumbnail destroyed
    /// </summary>
    public static void ThumbnailDestroyed(int slotId, IntPtr thumbHandle, string reason)
    {
        Log("THUMB", $"Destroyed: slot={slotId}, handle=0x{thumbHandle:X}, reason={reason}");
    }

    /// <summary>
    /// Log thumbnail refreshed/updated
    /// </summary>
    public static void ThumbnailRefreshed(int slotId, IntPtr thumbHandle, bool forceRecreate)
    {
        var action = forceRecreate ? "recreated" : "refreshed";
        Log("THUMB", $"Updated: slot={slotId}, handle=0x{thumbHandle:X}, action={action}");
    }

    /// <summary>
    /// Log thumbnail visibility change
    /// </summary>
    public static void ThumbnailVisibility(int slotId, bool visible, string reason)
    {
        Log("THUMB", $"Visibility: slot={slotId}, visible={visible}, reason={reason}");
    }

    /// <summary>
    /// Log thumbnail surface cleared
    /// </summary>
    public static void ThumbnailSurfaceCleared(int slotId)
    {
        Log("THUMB", $"Surface cleared: slot={slotId}");
    }

    #endregion

    #region Input Mapping

    /// <summary>
    /// Log input mapping calculation
    /// </summary>
    public static void InputMapping(int slotId, double dpiScale,
        int clientX, int clientY, int clientW, int clientH,
        int windowX, int windowY, int windowW, int windowH)
    {
        Log("INPUT", $"Mapping: slot={slotId}, dpi={dpiScale:F2}, " +
            $"client=({clientX},{clientY}) {clientW}x{clientH}, " +
            $"window=({windowX},{windowY}) {windowW}x{windowH}");
    }

    /// <summary>
    /// Log mapped point
    /// </summary>
    public static void InputMappedPoint(int slotId, double previewX, double previewY,
        int clientX, int clientY, bool valid)
    {
        var status = valid ? "OK" : "OUT_OF_BOUNDS";
        Log("INPUT", $"Point [{status}]: slot={slotId}, preview=({previewX:F1},{previewY:F1}) -> client=({clientX},{clientY})");
    }

    #endregion

    #region Loading/Zoning Detection

    /// <summary>
    /// Log loading/zoning detection
    /// </summary>
    public static void LoadingDetection(int slotId, bool isLoading,
        string titleSignal, bool rectStable, bool cpuSpike)
    {
        Log("LOADING", $"Detection: slot={slotId}, isLoading={isLoading}, " +
            $"title={titleSignal}, rectStable={rectStable}, cpuSpike={cpuSpike}");
    }

    /// <summary>
    /// Log loading state change
    /// </summary>
    public static void LoadingStateChanged(int slotId, bool wasLoading, bool isLoading)
    {
        Log("LOADING", $"StateChange: slot={slotId}, {wasLoading} -> {isLoading}");
    }

    #endregion

    #region Recovery Events

    /// <summary>
    /// Log recovery triggered
    /// </summary>
    public static void RecoveryTriggered(string trigger, int foregroundSlotId)
    {
        Log("RECOVERY", $"Triggered: reason={trigger}, expectedForeground={foregroundSlotId}");
    }

    /// <summary>
    /// Log recovery step
    /// </summary>
    public static void RecoveryStep(string step, bool success)
    {
        Log("RECOVERY", $"Step: {step}, success={success}");
    }

    /// <summary>
    /// Log recovery completed
    /// </summary>
    public static void RecoveryCompleted(bool success, string outcome)
    {
        Log("RECOVERY", $"Completed: success={success}, outcome={outcome}");
    }

    #endregion

    #region Layout Events

    /// <summary>
    /// Log layout application started
    /// </summary>
    public static void LayoutStarted(int foregroundSlotId, int activeSlotCount, string layoutName)
    {
        Log("LAYOUT", $"Started: foreground={foregroundSlotId}, slots={activeSlotCount}, layout={layoutName}");
    }

    /// <summary>
    /// Log layout application completed
    /// </summary>
    public static void LayoutCompleted(int foregroundSlotId, double elapsedMs, bool success)
    {
        Log("LAYOUT", $"Completed: foreground={foregroundSlotId}, elapsed={elapsedMs:F1}ms, success={success}");
    }

    /// <summary>
    /// Log layout skipped
    /// </summary>
    public static void LayoutSkipped(string reason)
    {
        Log("LAYOUT", $"Skipped: reason={reason}");
    }

    /// <summary>
    /// Log JMB-style swap
    /// </summary>
    public static void JmbSwap(int oldSlot, int newSlot, string method)
    {
        Log("LAYOUT", $"JMB Swap: {oldSlot} -> {newSlot}, method={method}");
    }

    #endregion

    #region Focus Events

    /// <summary>
    /// Log focus operation
    /// </summary>
    public static void FocusOperation(int slotId, IntPtr hwnd, string operation, bool success)
    {
        Log("FOCUS", $"Operation: slot={slotId}, hwnd=0x{hwnd:X}, op={operation}, success={success}");
    }

    #endregion
}
