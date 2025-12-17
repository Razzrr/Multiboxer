using System.Windows.Media;
using Multiboxer.Core.Slots;

namespace Multiboxer.Overlay;

/// <summary>
/// Manages overlay windows for all slots
/// </summary>
public class OverlayManager : IDisposable
{
    private readonly SlotManager _slotManager;
    private readonly Dictionary<int, OverlayWindow> _overlays = new();
    private readonly object _lock = new();
    private bool _disposed;

    private bool _showBorder = true;
    private bool _showNumber = true;
    private Color _borderColor = Colors.Red;
    private double _borderThickness = 3;

    /// <summary>
    /// Whether overlay borders are enabled
    /// </summary>
    public bool ShowBorder
    {
        get => _showBorder;
        set
        {
            _showBorder = value;
            UpdateAllOverlays();
        }
    }

    /// <summary>
    /// Whether overlay slot numbers are enabled
    /// </summary>
    public bool ShowNumber
    {
        get => _showNumber;
        set
        {
            _showNumber = value;
            UpdateAllOverlays();
        }
    }

    /// <summary>
    /// Border color for highlights
    /// </summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            UpdateAllOverlays();
        }
    }

    /// <summary>
    /// Border thickness
    /// </summary>
    public double BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = value;
            UpdateAllOverlays();
        }
    }

    public OverlayManager(SlotManager slotManager)
    {
        _slotManager = slotManager;

        // Subscribe to slot events
        _slotManager.SlotAdded += OnSlotAdded;
        _slotManager.SlotRemoved += OnSlotRemoved;
        _slotManager.SlotProcessExited += OnSlotProcessExited;

        // Create overlays for existing slots
        foreach (var slot in _slotManager.ActiveSlots)
        {
            CreateOverlay(slot);
        }
    }

    /// <summary>
    /// Create an overlay for a slot
    /// </summary>
    private void CreateOverlay(Slot slot)
    {
        if (slot.MainWindowHandle == IntPtr.Zero)
            return;

        lock (_lock)
        {
            if (_overlays.ContainsKey(slot.Id))
                return;

            var overlay = new OverlayWindow(slot)
            {
                ShowBorder = _showBorder,
                ShowNumber = _showNumber,
                BorderColor = _borderColor,
                HighlightBorderThickness = _borderThickness
            };

            _overlays[slot.Id] = overlay;
            overlay.Show();
        }
    }

    /// <summary>
    /// Remove an overlay for a slot
    /// </summary>
    private void RemoveOverlay(int slotId)
    {
        lock (_lock)
        {
            if (_overlays.TryGetValue(slotId, out var overlay))
            {
                overlay.Close();
                _overlays.Remove(slotId);
            }
        }
    }

    /// <summary>
    /// Update all overlays with current settings
    /// </summary>
    private void UpdateAllOverlays()
    {
        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsClosed)
                {
                    overlay.ShowBorder = _showBorder;
                    overlay.ShowNumber = _showNumber;
                    overlay.BorderColor = _borderColor;
                    overlay.HighlightBorderThickness = _borderThickness;
                }
            }
        }
    }

    /// <summary>
    /// Refresh all overlays
    /// </summary>
    public void RefreshAll()
    {
        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsClosed)
                {
                    overlay.Refresh();
                }
            }
        }
    }

    /// <summary>
    /// Show all overlays
    /// </summary>
    public void ShowAll()
    {
        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsClosed)
                {
                    overlay.Show();
                }
            }
        }
    }

    /// <summary>
    /// Hide all overlays
    /// </summary>
    public void HideAll()
    {
        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsClosed)
                {
                    overlay.Hide();
                }
            }
        }
    }

    private void OnSlotAdded(object? sender, SlotEventArgs e)
    {
        // Wait for the slot to have a window handle
        e.Slot.WindowChanged += OnSlotWindowChanged;

        if (e.Slot.MainWindowHandle != IntPtr.Zero)
        {
            CreateOverlay(e.Slot);
        }
    }

    private void OnSlotRemoved(object? sender, SlotEventArgs e)
    {
        e.Slot.WindowChanged -= OnSlotWindowChanged;
        RemoveOverlay(e.Slot.Id);
    }

    private void OnSlotProcessExited(object? sender, SlotEventArgs e)
    {
        RemoveOverlay(e.Slot.Id);
    }

    private void OnSlotWindowChanged(object? sender, SlotEventArgs e)
    {
        if (e.Slot.MainWindowHandle != IntPtr.Zero)
        {
            lock (_lock)
            {
                if (_overlays.TryGetValue(e.Slot.Id, out var overlay))
                {
                    overlay.SetTargetWindow(e.Slot.MainWindowHandle);
                }
                else
                {
                    CreateOverlay(e.Slot);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _slotManager.SlotAdded -= OnSlotAdded;
        _slotManager.SlotRemoved -= OnSlotRemoved;
        _slotManager.SlotProcessExited -= OnSlotProcessExited;

        lock (_lock)
        {
            foreach (var overlay in _overlays.Values)
            {
                overlay.Close();
            }
            _overlays.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
