using System.Windows;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Slots;

namespace Multiboxer.Overlay;

/// <summary>
/// Manages DWM thumbnail windows for Video FX-style display.
/// Shows live scaled views of game windows without resizing them.
/// </summary>
public class ThumbnailManager
{
    private readonly Dictionary<int, DwmThumbnailWindow> _thumbnails = new();
    private readonly object _lock = new();
    private bool _showLabels = true;
    private bool _showBorders = false;
    private int _foregroundSlotId;
    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

    /// <summary>
    /// Event raised when a thumbnail is clicked
    /// </summary>
    public event EventHandler<ThumbnailClickEventArgs>? ThumbnailClicked;

    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            _showLabels = value;
            UpdateAllThumbnails();
        }
    }

    public bool ShowBorders
    {
        get => _showBorders;
        set
        {
            _showBorders = value;
            UpdateAllThumbnails();
        }
    }

    /// <summary>
    /// Create or update a thumbnail for a slot
    /// </summary>
    public void SetThumbnail(int slotId, IntPtr sourceWindow, int x, int y, int width, int height)
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                if (!_thumbnails.TryGetValue(slotId, out var thumbnail) || thumbnail.IsClosed)
                {
                    // Create new thumbnail window
                    thumbnail = new DwmThumbnailWindow
                    {
                        SlotId = slotId,
                        ShowSlotLabel = _showLabels,
                        ShowBorder = _showBorders
                    };
                    // Subscribe to click events
                    thumbnail.ThumbnailClicked += OnThumbnailClicked;
                    _thumbnails[slotId] = thumbnail;
                }

                // Set position first
                thumbnail.SetPosition(x, y, width, height);
                thumbnail.ShowSlotLabel = _showLabels;
                thumbnail.ShowBorder = _showBorders;

                // Show the window BEFORE setting source (window needs a handle first)
                if (!thumbnail.IsVisible)
                {
                    thumbnail.Show();
                }

                // Now set the source - window handle is available after Show()
                thumbnail.SetSource(sourceWindow);
            }
        });
    }

    /// <summary>
    /// Remove thumbnail for a slot
    /// </summary>
    public void RemoveThumbnail(int slotId)
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                if (_thumbnails.TryGetValue(slotId, out var thumbnail))
                {
                    thumbnail.ThumbnailClicked -= OnThumbnailClicked;
                    if (!thumbnail.IsClosed)
                    {
                        thumbnail.Close();
                    }
                    _thumbnails.Remove(slotId);
                }
            }
        });
    }

    /// <summary>
    /// Handle thumbnail click - forward to subscribers
    /// </summary>
    private void OnThumbnailClicked(object? sender, ThumbnailClickEventArgs e)
    {
        ThumbnailClicked?.Invoke(this, e);
    }

    /// <summary>
    /// Hide thumbnail for a slot (e.g., when it's the foreground window)
    /// </summary>
    public void HideThumbnail(int slotId)
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                if (_thumbnails.TryGetValue(slotId, out var thumbnail) && !thumbnail.IsClosed)
                {
                    thumbnail.Hide();
                }
            }
        });
    }

    /// <summary>
    /// Show thumbnail for a slot
    /// </summary>
    public void ShowThumbnail(int slotId)
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                if (_thumbnails.TryGetValue(slotId, out var thumbnail) && !thumbnail.IsClosed)
                {
                    thumbnail.Show();
                }
            }
        });
    }

    /// <summary>
    /// Set which slot is in the foreground (its thumbnail will be hidden)
    /// </summary>
    public void SetForegroundSlot(int slotId)
    {
        lock (_lock)
        {
            // Show the previous foreground's thumbnail
            if (_foregroundSlotId > 0 && _foregroundSlotId != slotId)
            {
                ShowThumbnail(_foregroundSlotId);
            }

            _foregroundSlotId = slotId;

            // Hide the new foreground's thumbnail
            if (_foregroundSlotId > 0)
            {
                HideThumbnail(_foregroundSlotId);
            }
        }
    }

    /// <summary>
    /// Apply layout to all thumbnails
    /// </summary>
    public void ApplyLayout(IEnumerable<Slot> slots, IReadOnlyDictionary<int, SlotRegion> regions, int foregroundSlotId, int offsetX = 0, int offsetY = 0, Multiboxer.Core.Window.Rectangle? monitorBounds = null)
    {
        lock (_lock)
        {
            _foregroundSlotId = foregroundSlotId;

            Debug.WriteLine($"ThumbnailManager.ApplyLayout: foreground={foregroundSlotId}, offset=({offsetX},{offsetY})");

            foreach (var slot in slots)
            {
                if (slot.MainWindowHandle == IntPtr.Zero)
                    continue;

                if (!regions.TryGetValue(slot.Id, out var region))
                    continue;

                // Foreground slot doesn't get a thumbnail (it's the main window)
                if (slot.Id == foregroundSlotId)
                {
                    HideThumbnail(slot.Id);
                    continue;
                }

                // Background slots get thumbnails at their BackRegion position + monitor offset
                int x = region.BackRegion.X + offsetX;
                int y = region.BackRegion.Y + offsetY;

                // Clamp thumbnails to the chosen monitor bounds if provided
                if (monitorBounds.HasValue)
                {
                    var bounds = monitorBounds.Value;
                    x = Math.Max(bounds.Left, Math.Min(x, bounds.Right - region.BackRegion.Width));
                    y = Math.Max(bounds.Top, Math.Min(y, bounds.Bottom - region.BackRegion.Height));
                }

                Debug.WriteLine($"  Slot {slot.Id}: Creating thumbnail at ({x},{y}) {region.BackRegion.Width}x{region.BackRegion.Height}");

                SetThumbnail(
                    slot.Id,
                    slot.MainWindowHandle,
                    x,
                    y,
                    region.BackRegion.Width,
                    region.BackRegion.Height
                );
            }
        }
    }

    /// <summary>
    /// Hide all thumbnails
    /// </summary>
    public void HideAll()
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                foreach (var thumbnail in _thumbnails.Values)
                {
                    if (!thumbnail.IsClosed)
                    {
                        thumbnail.Hide();
                    }
                }
            }
        });
    }

    /// <summary>
    /// Show all thumbnails (except foreground)
    /// </summary>
    public void ShowAll()
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                foreach (var kvp in _thumbnails)
                {
                    if (kvp.Key != _foregroundSlotId && !kvp.Value.IsClosed)
                    {
                        kvp.Value.Show();
                    }
                }
            }
        });
    }

    /// <summary>
    /// Remove all thumbnails
    /// </summary>
    public void Clear()
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                foreach (var thumbnail in _thumbnails.Values)
                {
                    thumbnail.ThumbnailClicked -= OnThumbnailClicked;
                    if (!thumbnail.IsClosed)
                    {
                        thumbnail.Close();
                    }
                }
                _thumbnails.Clear();
            }
        });
    }

    /// <summary>
    /// Update all thumbnail settings
    /// </summary>
    private void UpdateAllThumbnails()
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                foreach (var thumbnail in _thumbnails.Values)
                {
                    if (!thumbnail.IsClosed)
                    {
                        thumbnail.ShowSlotLabel = _showLabels;
                        thumbnail.ShowBorder = _showBorders;
                    }
                }
            }
        });
    }

    /// <summary>
    /// Refresh all thumbnail source bindings
    /// </summary>
    public void RefreshAll()
    {
        ExecuteOnUi(() =>
        {
            lock (_lock)
            {
                foreach (var thumbnail in _thumbnails.Values)
                {
                    if (!thumbnail.IsClosed)
                    {
                        thumbnail.UpdateThumbnailProperties();
                    }
                }
            }
        });
    }

    private void ExecuteOnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }
}
