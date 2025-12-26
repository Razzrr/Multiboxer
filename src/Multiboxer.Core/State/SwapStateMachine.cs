using System.Diagnostics;
using Multiboxer.Core.Logging;

namespace Multiboxer.Core.State;

/// <summary>
/// States for the swap state machine
/// </summary>
public enum SwapState
{
    /// <summary>No swap in progress, ready for new requests</summary>
    Idle,

    /// <summary>A swap has been requested, waiting for debounce window</summary>
    SwapRequested,

    /// <summary>Layout is being applied to windows</summary>
    LayoutApplying,

    /// <summary>Thumbnails are being updated</summary>
    ThumbnailsApplying,

    /// <summary>Waiting for windows to stabilize after layout</summary>
    Stabilizing,

    /// <summary>A slot is in loading/zoning state, using safe swap path</summary>
    SafeSwapMode,

    /// <summary>Recovery routine is running</summary>
    Recovery
}

/// <summary>
/// Event args for swap completion
/// </summary>
public class SwapCompletedEventArgs : EventArgs
{
    public int SlotId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public SwapCompletedEventArgs(int slotId, bool success, string? error = null)
    {
        SlotId = slotId;
        Success = success;
        Error = error;
    }
}

/// <summary>
/// State machine for managing window swaps.
/// Implements queuing, debouncing, and coalescing to prevent catch-up behavior.
///
/// Key behaviors:
/// - Only the latest swap request is processed (coalescing)
/// - Rapid requests are debounced (150ms window)
/// - State transitions are explicit and logged
/// - Loading/zoning detection gates swap behavior
/// </summary>
public class SwapStateMachine
{
    private SwapState _state = SwapState.Idle;
    private readonly object _lock = new();

    // Swap request tracking
    private int? _pendingSwapSlotId;
    private int? _currentSwapSlotId;
    private DateTime _lastSwapRequest = DateTime.MinValue;
    private int _coalescedCount;

    // Timing configuration
    private readonly TimeSpan _debounceWindow = TimeSpan.FromMilliseconds(150);
    private readonly TimeSpan _stabilizeWindow = TimeSpan.FromMilliseconds(50);
    private readonly TimeSpan _maxSwapDuration = TimeSpan.FromMilliseconds(500);

    // Debounce timer
    private System.Timers.Timer? _debounceTimer;
    private System.Timers.Timer? _stabilizeTimer;

    // Events
    public event EventHandler<int>? SwapReady;
    public event EventHandler<SwapCompletedEventArgs>? SwapCompleted;
    public event EventHandler<SwapState>? StateChanged;

    /// <summary>
    /// Current state of the swap state machine
    /// </summary>
    public SwapState State
    {
        get
        {
            lock (_lock) return _state;
        }
    }

    /// <summary>
    /// The slot ID currently being swapped to (if any)
    /// </summary>
    public int? CurrentSwapSlotId
    {
        get
        {
            lock (_lock) return _currentSwapSlotId;
        }
    }

    /// <summary>
    /// Whether a swap is currently in progress
    /// </summary>
    public bool IsSwapping
    {
        get
        {
            lock (_lock)
            {
                return _state != SwapState.Idle;
            }
        }
    }

    public SwapStateMachine()
    {
        _debounceTimer = new System.Timers.Timer(_debounceWindow.TotalMilliseconds);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnDebounceElapsed;

        _stabilizeTimer = new System.Timers.Timer(_stabilizeWindow.TotalMilliseconds);
        _stabilizeTimer.AutoReset = false;
        _stabilizeTimer.Elapsed += OnStabilizeElapsed;
    }

    /// <summary>
    /// Request a swap to a specific slot.
    /// Multiple rapid requests are coalesced - only the latest target is used.
    /// </summary>
    /// <param name="slotId">The slot to swap to</param>
    /// <param name="isSlotLoading">Whether the target slot is in a loading/zoning state</param>
    /// <returns>True if the request was queued, false if rejected</returns>
    public bool RequestSwap(int slotId, bool isSlotLoading = false)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var timeSinceLastRequest = now - _lastSwapRequest;

            DebugLog.HotkeyPressed(slotId, _state.ToString(), now);

            // If we're in the middle of applying layout, queue the request
            if (_state == SwapState.LayoutApplying || _state == SwapState.ThumbnailsApplying)
            {
                // Coalesce: replace pending with new target
                if (_pendingSwapSlotId.HasValue && _pendingSwapSlotId.Value != slotId)
                {
                    _coalescedCount++;
                    DebugLog.SwapCoalesced(_pendingSwapSlotId.Value, slotId, _coalescedCount);
                }
                _pendingSwapSlotId = slotId;
                DebugLog.SwapQueueDecision(slotId, _pendingSwapSlotId, "queued (swap in progress)");
                return true;
            }

            // If in recovery, reject
            if (_state == SwapState.Recovery)
            {
                DebugLog.SwapQueueDecision(slotId, null, "rejected (recovery in progress)");
                return false;
            }

            // If we already have a pending request, coalesce
            if (_pendingSwapSlotId.HasValue)
            {
                if (_pendingSwapSlotId.Value != slotId)
                {
                    _coalescedCount++;
                    DebugLog.SwapCoalesced(_pendingSwapSlotId.Value, slotId, _coalescedCount);
                }
                _pendingSwapSlotId = slotId;
                _lastSwapRequest = now;

                // Reset debounce timer
                _debounceTimer?.Stop();
                _debounceTimer?.Start();

                DebugLog.SwapQueueDecision(slotId, _pendingSwapSlotId, "coalesced (updated target)");
                return true;
            }

            // New request
            _pendingSwapSlotId = slotId;
            _lastSwapRequest = now;
            _coalescedCount = 0;

            // If within debounce window of a previous swap, wait
            if (_state == SwapState.Stabilizing)
            {
                DebugLog.SwapDebounced(slotId, _debounceWindow.TotalMilliseconds);
                TransitionTo(SwapState.SwapRequested, "new request during stabilize");
                _debounceTimer?.Stop();
                _debounceTimer?.Start();
                return true;
            }

            // Start debounce timer for new request
            TransitionTo(SwapState.SwapRequested, "new swap request");
            _debounceTimer?.Stop();
            _debounceTimer?.Start();

            DebugLog.SwapQueueDecision(slotId, _pendingSwapSlotId, "accepted (debounce started)");
            return true;
        }
    }

    /// <summary>
    /// Called when the debounce timer elapses - time to process the swap
    /// </summary>
    private void OnDebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        int? slotToSwap;

        lock (_lock)
        {
            if (_pendingSwapSlotId == null)
            {
                TransitionTo(SwapState.Idle, "no pending request after debounce");
                return;
            }

            slotToSwap = _pendingSwapSlotId;
            _currentSwapSlotId = slotToSwap;
            _pendingSwapSlotId = null;

            if (_coalescedCount > 0)
            {
                DebugLog.SwapCoalesced(0, slotToSwap.Value, _coalescedCount);
            }

            TransitionTo(SwapState.LayoutApplying, $"processing swap to slot {slotToSwap}");
        }

        // Fire the SwapReady event to trigger actual layout application
        SwapReady?.Invoke(this, slotToSwap!.Value);
    }

    /// <summary>
    /// Called by the layout engine when layout application is complete
    /// </summary>
    public void NotifyLayoutComplete(bool success)
    {
        lock (_lock)
        {
            if (_state != SwapState.LayoutApplying)
            {
                Debug.WriteLine($"NotifyLayoutComplete called in unexpected state: {_state}");
                return;
            }

            if (success)
            {
                TransitionTo(SwapState.ThumbnailsApplying, "layout complete, updating thumbnails");
            }
            else
            {
                var slotId = _currentSwapSlotId ?? 0;
                _currentSwapSlotId = null;
                TransitionTo(SwapState.Idle, "layout failed");
                SwapCompleted?.Invoke(this, new SwapCompletedEventArgs(slotId, false, "Layout application failed"));
            }
        }
    }

    /// <summary>
    /// Called by the thumbnail manager when thumbnail updates are complete
    /// </summary>
    public void NotifyThumbnailsComplete(bool success)
    {
        lock (_lock)
        {
            if (_state != SwapState.ThumbnailsApplying)
            {
                Debug.WriteLine($"NotifyThumbnailsComplete called in unexpected state: {_state}");
                return;
            }

            TransitionTo(SwapState.Stabilizing, "thumbnails complete, stabilizing");

            // Start stabilize timer
            _stabilizeTimer?.Stop();
            _stabilizeTimer?.Start();
        }
    }

    /// <summary>
    /// Called when the stabilize timer elapses
    /// </summary>
    private void OnStabilizeElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        int completedSlotId;
        int? nextSlot = null;

        lock (_lock)
        {
            completedSlotId = _currentSwapSlotId ?? 0;
            _currentSwapSlotId = null;

            // Check if there's a pending request that came in during the swap
            if (_pendingSwapSlotId.HasValue)
            {
                nextSlot = _pendingSwapSlotId;
                _pendingSwapSlotId = null;
                _currentSwapSlotId = nextSlot;
                TransitionTo(SwapState.LayoutApplying, $"processing queued swap to slot {nextSlot}");
            }
            else
            {
                TransitionTo(SwapState.Idle, "swap complete");
            }
        }

        // Notify completion of the previous swap
        SwapCompleted?.Invoke(this, new SwapCompletedEventArgs(completedSlotId, true));

        // If there's a queued swap, process it
        if (nextSlot.HasValue)
        {
            SwapReady?.Invoke(this, nextSlot.Value);
        }
    }

    /// <summary>
    /// Enter recovery mode
    /// </summary>
    public void EnterRecovery(string reason)
    {
        lock (_lock)
        {
            _pendingSwapSlotId = null;
            _debounceTimer?.Stop();
            _stabilizeTimer?.Stop();
            TransitionTo(SwapState.Recovery, reason);
        }
    }

    /// <summary>
    /// Exit recovery mode
    /// </summary>
    public void ExitRecovery(bool success)
    {
        lock (_lock)
        {
            TransitionTo(SwapState.Idle, success ? "recovery successful" : "recovery failed");
        }
    }

    /// <summary>
    /// Force reset to idle state (emergency use only)
    /// </summary>
    public void ForceReset()
    {
        lock (_lock)
        {
            _pendingSwapSlotId = null;
            _currentSwapSlotId = null;
            _debounceTimer?.Stop();
            _stabilizeTimer?.Stop();
            TransitionTo(SwapState.Idle, "force reset");
        }
    }

    /// <summary>
    /// Cancel any pending swap
    /// </summary>
    public void CancelPending()
    {
        lock (_lock)
        {
            if (_pendingSwapSlotId.HasValue)
            {
                DebugLog.SwapQueueDecision(_pendingSwapSlotId.Value, null, "cancelled");
                _pendingSwapSlotId = null;
            }

            if (_state == SwapState.SwapRequested)
            {
                _debounceTimer?.Stop();
                TransitionTo(SwapState.Idle, "pending cancelled");
            }
        }
    }

    private void TransitionTo(SwapState newState, string reason)
    {
        var oldState = _state;
        _state = newState;

        DebugLog.StateTransition(oldState.ToString(), newState.ToString(), reason);
        StateChanged?.Invoke(this, newState);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _stabilizeTimer?.Stop();
        _stabilizeTimer?.Dispose();
    }
}
