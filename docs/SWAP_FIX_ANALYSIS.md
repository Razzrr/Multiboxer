# EQBZ Multiboxer - Swap, Preview, and Input Fix Analysis

## Executive Summary

This document provides a comprehensive analysis of the reported bugs, compares the implementation to Joe Multiboxer (JMB), and outlines the implementation plan for fixes.

---

## 1. ROOT CAUSE HYPOTHESES

### Bug 1: Cannot reliably swap during loading/zoning; catch-up behavior

**Symptoms:** Swap hotkeys during zoning cause the app to "play catch-up" after zoning completes.

**Root Cause:**
- `FocusAndApplyLayoutAsync()` in `App.xaml.cs:296` is called for every hotkey press without queuing or coalescing
- Each async call runs independently - if 5 F1 presses happen during zoning, 5 layout applications queue up
- No detection of loading/zoning state means all requests are processed
- The `async` nature means they can stack and execute sequentially after the zone loads

**Evidence in Code:**
```csharp
// App.xaml.cs:176-194
private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
{
    if (e.SlotId.HasValue)
    {
        // NO QUEUING - every press triggers this
        _ = FocusAndApplyLayoutAsync(slotId);  // Fire and forget!
    }
}
```

**JMB Comparison:** JMB uses event-driven approach where `windowvisibility foreground` is called once, and the layout responds to `OnActivate` or `OnHotkeyFocused` events. The layout system has an `Applied` flag that prevents redundant applications.

---

### Bug 2: Preview layering issues while loading

**Symptoms:** Not all previews show immediately; windows half-clipped into preview region.

**Root Cause:**
- `ThumbnailManager.ApplyLayout()` in `ThumbnailManager.cs:175` iterates through slots and creates/updates thumbnails
- No synchronization between window positioning (`LayoutEngine.ApplyLayoutWithMain`) and thumbnail creation
- During loading, window handles may be stale or windows may be in transition
- DWM thumbnails are registered against window handles that may become invalid during loading

**Evidence in Code:**
```csharp
// ThumbnailManager.cs:175-221
public void ApplyLayout(IEnumerable<Slot> slots, ...)
{
    foreach (var slot in slots)
    {
        // No validation that window is in stable state
        if (slot.MainWindowHandle == IntPtr.Zero)
            continue;
        // Creates thumbnail even if window is mid-transition
        SetThumbnail(...);
    }
}
```

**JMB Comparison:** JMB doesn't use DWM thumbnails for preview - it uses actual window resizing with `-stealth` flag. Each session manages its own window, avoiding central thumbnail coordination issues.

---

### Bug 3: Hangover/stale frame bleed-through

**Symptoms:** When looking at screen1, screen2 bleeds through because previous frame is shown.

**Root Cause:**
- `DwmThumbnailWindow.SetSource()` in `DwmThumbnailWindow.xaml.cs:122-168` reuses the same thumbnail handle if the source window hasn't changed
- When swapping, the thumbnail for the OLD foreground needs to be recreated, but the code only refreshes properties
- The DWM thumbnail system caches frames; no explicit invalidation/clear happens on swap

**Evidence in Code:**
```csharp
// DwmThumbnailWindow.xaml.cs:134-139
if (_thumbnailHandle != IntPtr.Zero && _sourceWindow == sourceWindowHandle)
{
    // Source unchanged; just refresh properties
    UpdateThumbnailProperties();  // Doesn't clear the frame!
    return true;
}
```

**Fix Required:** Force thumbnail recreation (unregister + re-register) when a slot transitions from foreground to background, or clear the WPF surface before updating.

---

### Bug 4: Slot 1/2 overhang or incorrect sizing, requires double hotkey press

**Symptoms:** Windows size incorrectly and only correct after pressing hotkey again.

**Root Cause:**
- `LayoutEngine.ApplyLayoutWithMain()` in `LayoutEngine.cs:211-246` has a fast path (`JmbStyleSwap`) that uses `SWP_NOSIZE` to avoid resize
- The `JmbStyleSwap` method parks the old foreground off-screen WITHOUT resizing, then brings the new foreground to ForeRegion WITH sizing
- However, windows may not be at the correct size initially (first time setup vs subsequent swaps)
- The `_initialLayoutApplied` flag determines which path is taken, but doesn't account for windows that haven't been sized yet

**Evidence in Code:**
```csharp
// LayoutEngine.cs:236-246
if (_initialLayoutApplied && _options.UseThumbnails && ...)
{
    // JMB-style swap: uses SWP_NOSIZE for parking
    JmbStyleSwap(_currentForegroundSlotId, mainSlotId, activeSlots);
    return;
}
// Full layout only runs first time
SwapLayoutOnFocus(mainSlotId);
```

**Problem:** If `_initialLayoutApplied` is true but a window was never properly sized (e.g., attached mid-session), the JmbStyleSwap path won't correct it.

**JMB Comparison:** JMB always applies size when bringing to foreground (`-size -viewable ${mainWidth}x${mainHeight}`). The `-stealth` flag prevents visual artifacts but size is always set.

---

### Bug 5: Mouse cursor/input offset by ~1 inch, self-corrects after swapping

**Symptoms:** Mouse clicks are offset on one account, then correct themselves.

**Root Cause:**
- No explicit input mapping/coordinate transformation in the codebase
- `DwmThumbnailWindow.SetPosition()` in `DwmThumbnailWindow.xaml.cs:231-243` converts physical pixels to DIPs using `GetDpiScale()`
- The DPI scale is queried from `PresentationSource.FromVisual(this)` which may be stale or incorrect after monitor changes
- When a window moves between monitors with different DPI, the scale factor isn't refreshed

**Evidence in Code:**
```csharp
// DwmThumbnailWindow.xaml.cs:248-260
private double GetDpiScale()
{
    var source = PresentationSource.FromVisual(this);
    if (source?.CompositionTarget != null)
    {
        return source.CompositionTarget.TransformToDevice.M11;
    }
    // Fallback is wrong - uses primary screen ratio
    return System.Windows.SystemParameters.PrimaryScreenHeight /
           System.Windows.SystemParameters.WorkArea.Height;
}
```

**The fallback calculation is incorrect** - it should use the actual DPI, not a ratio of screen dimensions.

---

### Bug 6: Tabbing to window interrupts camping; refresh closes vendor windows

**Symptoms:** Leaving a screen causes refresh that closes vendor windows and stops camping.

**Root Cause:**
- When focus changes, `OnSlotActivated` in `App.xaml.cs:214-289` runs and may call `LayoutEngine.ApplyLayoutWithMain()`
- `SwapLayoutOnFocus()` in `LayoutEngine.cs:348-529` does:
  1. `MakeBorderless()` - modifies window styles
  2. `SetWindowPositionsBatched()` - moves/resizes windows
  3. `ForceForegroundWindow()` - calls `SetForegroundWindow`, `SetFocus`
- The style changes and SetFocus calls can trigger EQ client's window message handlers
- EQ interprets these as user interaction, potentially closing UI windows

**Evidence in Code:**
```csharp
// LayoutEngine.cs:401-431
if (_options.MakeBorderless)
{
    foreach (var slot in activeSlots)
    {
        // This modifies window styles on EVERY swap
        WindowHelper.MakeBorderless(hwnd);
    }
    Thread.Sleep(30);  // Delay but still causes window messages
}
```

**JMB Comparison:** JMB uses `windowcharacteristics -lock` at startup to lock window characteristics, then uses `-stealth` flag for resizes. The `-stealth` flag specifically avoids triggering the game's window event handlers.

---

### Bug 7: After camping, swaps show only previews, no main window

**Symptoms:** Character moves but window isn't visible in main area.

**Root Cause:**
- When parking windows off-screen, `JmbStyleSwap()` in `LayoutEngine.cs:253-338` parks at negative coordinates
- If the swap partially fails (e.g., `SetForegroundWindow` fails because EQ is camping), the "new foreground" stays parked
- No recovery mechanism to detect this state and re-apply layout

**Evidence in Code:**
```csharp
// LayoutEngine.cs:271-276
var virtualBounds = MonitorManager.GetVirtualScreenBounds();
int parkX = virtualBounds.Left - 2000;
int parkY = virtualBounds.Top - 2000;
// Old window parked here, but if new window swap fails...
// ...the old window is off-screen and new window never came to foreground
```

---

## 2. JOE MULTIBOXER vs MY CODE COMPARISON

| Aspect | My Code | Joe Multiboxer | Why Joe is Safer | Change Required |
|--------|---------|----------------|------------------|-----------------|
| **Hotkey Handling** | `FocusAndApplyLayoutAsync()` called per press, no queuing | `OnSlotHotkey()` sends `uplink focus`, fires `OnHotkeyFocused` event | Event-driven prevents stacking; single handler | Add swap queue with debounce |
| **Swap Queuing** | None - every hotkey press triggers layout | Implicit via event system - only latest focus matters | No catch-up behavior | Implement `SwapStateMachine` with queue coalescing |
| **Loading Detection** | None - `Slot.State` exists but not used for gating | No explicit detection but `-stealth` flag minimizes impact | `-stealth` prevents DirectX resets | Add loading heuristics, use SWP_NOCOPYBITS |
| **Layout Application** | Two paths: `SwapLayoutOnFocus` (full) vs `JmbStyleSwap` (optimized) | Single path: `ApplyWindowLayout()` always sets size | Consistent behavior regardless of state | Always apply size, use `-stealth` equivalent |
| **Borderless** | Applied on EVERY swap via `MakeBorderless()` | Applied once via `windowcharacteristics -lock` at init | No redundant style changes | Cache borderless state, apply once |
| **Window Positioning** | `DeferWindowPos` batch for all windows | `relay jmb${slotID}` - each session positions itself | Distributed; no single point of failure | Consider applying to current window first, then others |
| **Thumbnail Lifecycle** | Reuse handle if source unchanged | N/A - JMB doesn't use DWM thumbnails | N/A | Force recreation on foreground transition |
| **Focus Management** | `ForceForegroundWindow()` with `AttachThreadInput` + `SetFocus` | Simple `windowvisibility foreground` or `SetForegroundWindow` | Less aggressive; fewer window messages | Remove SetFocus, reduce AttachThreadInput usage |
| **DPI Handling** | `GetDpiScale()` with incorrect fallback | Per-session handling; each window knows its monitor | No central DPI calculation | Fix fallback, refresh DPI on monitor change |
| **Recovery** | None | N/A - distributed architecture self-recovers | Each session independent | Add recovery routine to detect and fix broken state |

---

## 3. JOE MULTIBOXER PIPELINE SUMMARY

### Hotkey Flow:
1. Global hotkey registered via `globalbind "focus"`
2. Hotkey fires → `OnGlobalHotkey()` → `windowvisibility foreground` + `Event[OnHotkeyFocused]:Execute`
3. Slot-specific hotkey → `OnSlotHotkey(numSlot)` → `uplink focus "jmb${numSlot}"` + relay event

### Layout Application:
1. Event received (`On Activate` or `OnHotkeyFocused`)
2. `ApplyWindowLayout(setOtherSlots)` called
3. Current window positioned via `WindowCharacteristics -pos -viewable ... -size -viewable ... -frame none`
4. If `setOtherSlots=TRUE`, relay commands to other sessions:
   ```
   relay jmb${slotID} "WindowCharacteristics ${stealthFlag}-pos ... -size ... -frame none"
   ```
5. `-stealth` flag used when rescaling to prevent visual artifacts

### Key Flags:
- `swapOnActivate` - respond to Windows activation
- `swapOnHotkeyFocused` - respond to hotkey focus
- `leaveHole` - preserve foreground slot's position in strip
- `rescaleWindows` - resize background windows or just move them
- `-stealth` - apply changes without triggering game events

---

## 4. IMPLEMENTATION PLAN

### Phase 1: State Machine and Swap Coalescing

Create `SwapStateMachine.cs`:
```csharp
public enum SwapState
{
    Idle,
    SwapRequested,
    LayoutApplying,
    ThumbnailsApplying,
    Stabilizing,
    BlockedLoadingOrZoning,
    Recovery
}

public class SwapStateMachine
{
    private SwapState _state = SwapState.Idle;
    private int? _pendingSwapSlotId;
    private DateTime _lastSwapRequest;
    private readonly TimeSpan _debounceWindow = TimeSpan.FromMilliseconds(150);

    public void RequestSwap(int slotId);
    public void ProcessQueue();
    private void TransitionTo(SwapState newState);
}
```

### Phase 2: Loading/Zoning Detection

Add to `Slot.cs`:
```csharp
public bool IsLoadingOrZoning { get; private set; }
public DateTime LastWindowRectChange { get; private set; }
public DateTime LastTitleChange { get; private set; }

public void UpdateLoadingState()
{
    // Heuristics:
    // 1. Title contains "Loading" or changes frequently
    // 2. Window rect hasn't changed in >100ms (stable)
    // 3. Process CPU spike without rect changes
}
```

### Phase 3: Atomic Layout Application

Modify `LayoutEngine.cs`:
```csharp
public void ApplyLayoutAtomic(int mainSlotId)
{
    // 1. Validate all window handles are valid
    // 2. Apply borderless ONCE (check cache)
    // 3. Calculate all positions
    // 4. Apply foreground window first (position + size)
    // 5. Apply background windows (position only if thumbnails enabled)
    // 6. Verify positions match expected
    // 7. Single retry on mismatch
    // 8. Update thumbnails AFTER positions confirmed
}
```

### Phase 4: Thumbnail Lifecycle Fix

Modify `ThumbnailManager.cs`:
```csharp
public void ApplyLayout(..., bool forceRecreate = false)
{
    // When slot transitions from foreground to background:
    // 1. Clear the WPF surface
    // 2. Unregister the DWM thumbnail
    // 3. Re-register with fresh handle
    // This prevents stale frame bleed-through
}
```

Modify `DwmThumbnailWindow.xaml.cs`:
```csharp
public void ForceRecreate()
{
    UnregisterThumbnail();
    // Small delay for DWM to process
    _thumbnailHandle = IntPtr.Zero;
    _sourceWindow = IntPtr.Zero;
}

public void ClearSurface()
{
    // Set background to solid color before new source
    ThumbnailBorder.Background = Brushes.Black;
}
```

### Phase 5: Fix Input Mapping

Add `InputMapper.cs`:
```csharp
public class InputMapper
{
    public void RefreshMapping(int slotId, IntPtr hwnd)
    {
        // 1. Get monitor DPI via GetDpiForWindow
        // 2. Get client rect via GetClientRect
        // 3. Get window rect via GetWindowRect
        // 4. Calculate offset = window rect - client rect (accounts for borders)
        // 5. Store transform matrix for this slot
        // 6. Validate by mapping center point
    }

    public Point MapPreviewToClient(int slotId, Point previewPoint)
    {
        // Apply stored transform
    }
}
```

### Phase 6: Prevent Camping/Vendor Interruption

Modify `LayoutEngine.cs`:
```csharp
private void ApplyForegroundWindow(Slot slot, SlotRegion region)
{
    // Use minimal flags to avoid triggering game events
    var flags = SetWindowPosFlags.SWP_NOACTIVATE |
                SetWindowPosFlags.SWP_NOCOPYBITS |  // Key for DirectX
                SetWindowPosFlags.SWP_ASYNCWINDOWPOS;

    // Don't call SetFocus - just SetForegroundWindow once
    // Don't modify window styles unless necessary
}
```

### Phase 7: Recovery System

Add to `App.xaml.cs`:
```csharp
private void RunRecovery()
{
    // 1. Check if foreground slot's window is visible on any monitor
    // 2. If not, find where it actually is
    // 3. Re-apply layout with forced positions
    // 4. Recreate all thumbnails
    // 5. Log outcome
}

private System.Timers.Timer _recoveryTimer;
private void CheckForBrokenState()
{
    // Runs every 2 seconds
    // Detects if foreground window is off-screen but slot says it's foreground
}
```

---

## 5. DETAILED LOGGING IMPLEMENTATION

Add `MultiboxerDebugLog.cs`:
```csharp
public static class DebugLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Multiboxer", "multiboxer_debug.log");

    public static void HotkeyPressed(int slotId, SwapState currentState);
    public static void SwapQueueDecision(int slotId, string decision);
    public static void StateTransition(SwapState from, SwapState to, string reason);
    public static void WindowMetrics(int slotId, IntPtr hwnd, string phase);
    public static void ThumbnailEvent(int slotId, string eventType);
    public static void InputMapping(int slotId, double dpi, RECT client);
    public static void LoadingDetection(int slotId, bool isLoading, string signals);
    public static void Recovery(string trigger, string steps, bool success);
}
```

---

## 6. TEST PLAN

### Scenario 1: Rapid Swapping During Zoning
1. Zone a character (travel to another zone)
2. While "Loading, please wait" is shown, spam F1-F2-F3-F1-F2 rapidly
3. **Expected:** After zone loads, only the LAST requested slot (F2) is foreground
4. **Verify:** No catch-up behavior, single layout application

### Scenario 2: Swapping During Login Load
1. Start EQ, at character select
2. Click Enter World while spamming F1-F2
3. **Expected:** Final swap is to the last requested slot
4. **Verify:** No layout errors, window sized correctly

### Scenario 3: Tab Away and Return
1. Have 3 slots running, F1 as foreground
2. Alt+Tab to another application (e.g., browser)
3. Alt+Tab back or click the EQ taskbar icon
4. **Expected:** F1 is still foreground, layout unchanged
5. **Verify:** No vendor windows closed, no camping interrupted

### Scenario 4: Multi-Monitor with Negative Coordinates
1. Configure secondary monitor LEFT of primary (negative X coordinates)
2. Set layout target to secondary monitor
3. Launch 3 slots, apply layout
4. Swap between F1-F2-F3
5. **Expected:** All windows position correctly on secondary monitor
6. **Verify:** Thumbnails appear in correct positions

### Scenario 5: Mixed DPI Monitors
1. Primary at 100% (96 DPI), Secondary at 150% (144 DPI)
2. Target secondary monitor for layout
3. Click thumbnails to swap
4. **Expected:** Mouse clicks land on correct UI elements
5. **Verify:** No offset, even after multiple swaps

### Scenario 6: No Camping Interruption
1. Slot 1 is in-game, start /camp
2. While camping countdown runs, press F2 to switch to slot 2
3. **Expected:** Slot 1's camping continues, slot 2 becomes foreground
4. **Verify:** Slot 1's camp timer reaches zero normally

### Scenario 7: Recovery from Broken State
1. Force a broken state by moving windows manually during swap
2. Wait 2-3 seconds
3. **Expected:** Recovery routine detects and fixes layout
4. **Verify:** Foreground window visible, thumbnails correct

---

## 7. ACCEPTANCE CRITERIA

| Bug | Acceptance Criteria |
|-----|---------------------|
| 1 | Single hotkey press during zoning results in correct final state; no catch-up |
| 2 | All preview thumbnails visible immediately after swap; no half-clipped windows |
| 3 | No stale frame bleed-through; previous screen never visible after swap |
| 4 | One hotkey press always results in correct window size; no double-press needed |
| 5 | Mouse offset never exceeds 5 pixels on any monitor configuration |
| 6 | Camping continues through swaps; vendor windows stay open |
| 7 | Automatic recovery restores working state within 5 seconds |

---

## 8. FILES TO MODIFY

### New Files:
- `src/Multiboxer.Core/State/SwapStateMachine.cs`
- `src/Multiboxer.Core/Input/InputMapper.cs`
- `src/Multiboxer.Core/Logging/DebugLog.cs`

### Modified Files:
- `src/Multiboxer.App/App.xaml.cs` - Wire up state machine, recovery timer
- `src/Multiboxer.Core/Layout/LayoutEngine.cs` - Atomic layout, remove redundant borderless
- `src/Multiboxer.Core/Slots/Slot.cs` - Loading detection
- `src/Multiboxer.Core/Slots/SlotManager.cs` - Expose loading state
- `src/Multiboxer.Overlay/ThumbnailManager.cs` - Force recreation, clear surface
- `src/Multiboxer.Overlay/DwmThumbnailWindow.xaml.cs` - ForceRecreate, ClearSurface
- `src/Multiboxer.Core/Window/WindowHelper.cs` - Minimal flags for positioning

---

## 9. IMPLEMENTATION ORDER

1. **DebugLog.cs** - Enable detailed logging first
2. **SwapStateMachine.cs** - Core state management and queuing
3. **Slot.cs loading detection** - Heuristics for zoning state
4. **LayoutEngine.cs atomic layout** - Fix sizing and borderless caching
5. **ThumbnailManager.cs lifecycle** - Fix stale frame issue
6. **DwmThumbnailWindow.cs** - Force recreation and surface clear
7. **InputMapper.cs** - DPI-aware coordinate mapping
8. **App.xaml.cs integration** - Wire everything together
9. **Recovery system** - Detect and fix broken states
10. **Testing and validation** - Run all test scenarios

