using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Multiboxer.Core.Config;

namespace Multiboxer.Core.Slots;

/// <summary>
/// Manages all multiboxing slots
/// </summary>
public partial class SlotManager : ObservableObject, IDisposable
{
    private readonly ConcurrentDictionary<int, Slot> _slots = new();
    private readonly object _lock = new();
    private bool _disposed;
    private int _foregroundSlotId;

    /// <summary>
    /// Maximum number of slots supported
    /// </summary>
    public const int MaxSlots = 72;

    /// <summary>
    /// Observable collection of active slots for UI binding
    /// </summary>
    public ObservableCollection<Slot> ActiveSlots { get; } = new();

    /// <summary>
    /// The currently focused slot ID (0 if none)
    /// </summary>
    [ObservableProperty]
    private int _currentSlotId;

    /// <summary>
    /// The currently focused slot (null if none)
    /// </summary>
    public Slot? FocusedSlot => CurrentSlotId > 0 ? GetSlot(CurrentSlotId) : null;

    /// <summary>
    /// Total number of active slots
    /// </summary>
    public int ActiveSlotCount => ActiveSlots.Count;

    /// <summary>
    /// Event raised when a slot is added
    /// </summary>
    public event EventHandler<SlotEventArgs>? SlotAdded;

    /// <summary>
    /// Event raised when a slot is removed
    /// </summary>
    public event EventHandler<SlotEventArgs>? SlotRemoved;

    /// <summary>
    /// Event raised when the foreground slot changes
    /// </summary>
    public event EventHandler<SlotEventArgs>? ForegroundSlotChanged;

    /// <summary>
    /// Event raised when any slot's process exits
    /// </summary>
    public event EventHandler<SlotEventArgs>? SlotProcessExited;

    /// <summary>
    /// Event raised when a slot becomes active (window attached)
    /// Used to apply borderless mode and update layout
    /// </summary>
    public event EventHandler<SlotEventArgs>? SlotActivated;

    /// <summary>
    /// Get or create a slot by ID
    /// </summary>
    public Slot GetOrCreateSlot(int slotId)
    {
        if (slotId < 1 || slotId > MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot ID must be between 1 and {MaxSlots}");

        return _slots.GetOrAdd(slotId, id =>
        {
            var slot = new Slot(id);
            slot.ProcessExited += OnSlotProcessExited;
            slot.StateChanged += OnSlotStateChanged;

            lock (_lock)
            {
                ActiveSlots.Add(slot);
            }

            SlotAdded?.Invoke(this, new SlotEventArgs(slot));
            OnPropertyChanged(nameof(ActiveSlotCount));

            return slot;
        });
    }

    /// <summary>
    /// Get a slot by ID if it exists
    /// </summary>
    public Slot? GetSlot(int slotId)
    {
        return _slots.TryGetValue(slotId, out var slot) ? slot : null;
    }

    /// <summary>
    /// Get all active slots
    /// </summary>
    public IEnumerable<Slot> GetActiveSlots()
    {
        return _slots.Values.Where(s => s.HasProcess).OrderBy(s => s.Id);
    }

    /// <summary>
    /// Get the next available slot ID
    /// </summary>
    public int GetNextAvailableSlotId()
    {
        for (int i = 1; i <= MaxSlots; i++)
        {
            if (!_slots.ContainsKey(i) || !_slots[i].HasProcess)
                return i;
        }
        return -1; // No available slots
    }

    /// <summary>
    /// Launch a process into the next available slot
    /// </summary>
    public async Task<Slot?> LaunchAsync(LaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var slotId = GetNextAvailableSlotId();
        if (slotId < 0)
            return null;

        return await LaunchAsync(slotId, profile, cancellationToken);
    }

    /// <summary>
    /// Launch a process into a specific slot
    /// </summary>
    public async Task<Slot?> LaunchAsync(int slotId, LaunchProfile profile, CancellationToken cancellationToken = default)
    {
        var slot = GetOrCreateSlot(slotId);

        if (slot.HasProcess)
        {
            throw new InvalidOperationException($"Slot {slotId} already has a running process");
        }

        var success = await slot.LaunchAsync(profile, cancellationToken);

        if (success)
        {
            // Fire event so layout/borderless can be applied
            SlotActivated?.Invoke(this, new SlotEventArgs(slot));
        }

        return success ? slot : null;
    }

    /// <summary>
    /// Attach an existing process to a slot
    /// </summary>
    public Slot AttachProcess(int slotId, Process process)
    {
        var slot = GetOrCreateSlot(slotId);
        slot.AttachProcess(process);

        // Fire event so layout/borderless can be applied
        SlotActivated?.Invoke(this, new SlotEventArgs(slot));

        return slot;
    }

    /// <summary>
    /// Attach an existing process by window handle
    /// </summary>
    public Slot? AttachWindow(int slotId, IntPtr windowHandle)
    {
        Native.User32.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0)
            return null;

        try
        {
            var process = Process.GetProcessById((int)processId);
            var slot = GetOrCreateSlot(slotId);
            slot.AttachProcess(process);

            // Fire event so layout/borderless can be applied
            SlotActivated?.Invoke(this, new SlotEventArgs(slot));

            return slot;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Remove a slot
    /// </summary>
    public bool RemoveSlot(int slotId)
    {
        if (_slots.TryRemove(slotId, out var slot))
        {
            slot.ProcessExited -= OnSlotProcessExited;
            slot.StateChanged -= OnSlotStateChanged;

            lock (_lock)
            {
                ActiveSlots.Remove(slot);
            }

            slot.Dispose();
            SlotRemoved?.Invoke(this, new SlotEventArgs(slot));
            OnPropertyChanged(nameof(ActiveSlotCount));

            return true;
        }
        return false;
    }

    /// <summary>
    /// Close all slots
    /// </summary>
    public void CloseAll()
    {
        foreach (var slot in _slots.Values.ToList())
        {
            slot.Close();
            RemoveSlot(slot.Id);
        }
    }

    /// <summary>
    /// Switch focus to a specific slot
    /// </summary>
    public bool FocusSlot(int slotId)
    {
        var slot = GetSlot(slotId);
        if (slot == null || !slot.HasProcess)
            return false;

        var success = slot.Focus();
        if (success)
        {
            UpdateForegroundSlot(slotId);
        }
        return success;
    }

    /// <summary>
    /// Switch to the next slot (cycle through)
    /// </summary>
    public bool FocusNextSlot()
    {
        var activeSlots = GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
            return false;

        var currentIndex = activeSlots.FindIndex(s => s.Id == CurrentSlotId);
        var nextIndex = (currentIndex + 1) % activeSlots.Count;

        return FocusSlot(activeSlots[nextIndex].Id);
    }

    /// <summary>
    /// Switch to the previous slot (cycle through)
    /// </summary>
    public bool FocusPreviousSlot()
    {
        var activeSlots = GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
            return false;

        var currentIndex = activeSlots.FindIndex(s => s.Id == CurrentSlotId);
        var prevIndex = currentIndex <= 0 ? activeSlots.Count - 1 : currentIndex - 1;

        return FocusSlot(activeSlots[prevIndex].Id);
    }

    /// <summary>
    /// Swap the foreground slot with another slot's position
    /// </summary>
    public void SwapSlotToForeground(int slotId)
    {
        var slot = GetSlot(slotId);
        if (slot == null)
            return;

        var previousForeground = GetSlot(_foregroundSlotId);

        // This is used by the layout engine to determine which window
        // should be in the "main" position
        UpdateForegroundSlot(slotId);
    }

    /// <summary>
    /// Update window information for all slots
    /// </summary>
    public void RefreshAllWindowInfo()
    {
        foreach (var slot in _slots.Values)
        {
            slot.RefreshWindowHandle();
            slot.UpdateWindowInfo();
        }
    }

    /// <summary>
    /// Check and update which slot is currently in the foreground
    /// </summary>
    public void UpdateForegroundFromSystem()
    {
        var foregroundWindow = Native.User32.GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return;

        foreach (var slot in _slots.Values)
        {
            if (slot.MainWindowHandle == foregroundWindow)
            {
                UpdateForegroundSlot(slot.Id);
                return;
            }
        }

        // Foreground window is not one of our slots
        UpdateForegroundSlot(0);
    }

    private void UpdateForegroundSlot(int slotId)
    {
        if (_foregroundSlotId == slotId)
            return;

        var previousSlot = GetSlot(_foregroundSlotId);
        if (previousSlot != null)
        {
            previousSlot.State = SlotState.Running;
        }

        _foregroundSlotId = slotId;
        CurrentSlotId = slotId;

        var newSlot = GetSlot(slotId);
        if (newSlot != null)
        {
            newSlot.State = SlotState.Foreground;
            ForegroundSlotChanged?.Invoke(this, new SlotEventArgs(newSlot));
        }
    }

    private void OnSlotProcessExited(object? sender, SlotEventArgs e)
    {
        SlotProcessExited?.Invoke(this, e);
    }

    private void OnSlotStateChanged(object? sender, SlotEventArgs e)
    {
        // Could trigger layout updates here if needed
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var slot in _slots.Values)
        {
            slot.ProcessExited -= OnSlotProcessExited;
            slot.StateChanged -= OnSlotStateChanged;
            slot.Dispose();
        }

        _slots.Clear();
        ActiveSlots.Clear();

        GC.SuppressFinalize(this);
    }
}
