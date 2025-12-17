using System.Text.Json.Serialization;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Options controlling layout behavior
/// </summary>
public class LayoutOptions
{
    /// <summary>
    /// Swap windows when clicking/activating a background window
    /// </summary>
    [JsonPropertyName("swapOnActivate")]
    public bool SwapOnActivate { get; set; } = false;

    /// <summary>
    /// Swap windows when using hotkey to focus a slot
    /// </summary>
    [JsonPropertyName("swapOnHotkeyFocused")]
    public bool SwapOnHotkeyFocused { get; set; } = true;

    /// <summary>
    /// When swapping, leave the main region empty instead of moving the previous main there
    /// </summary>
    [JsonPropertyName("leaveHole")]
    public bool LeaveHole { get; set; }

    /// <summary>
    /// Avoid placing windows over the taskbar
    /// </summary>
    [JsonPropertyName("avoidTaskbar")]
    public bool AvoidTaskbar { get; set; } = true;

    /// <summary>
    /// Remove window borders when applying layout
    /// </summary>
    [JsonPropertyName("makeBorderless")]
    public bool MakeBorderless { get; set; } = true;

    /// <summary>
    /// Scale windows smoothly instead of snapping
    /// </summary>
    [JsonPropertyName("rescaleWindows")]
    public bool RescaleWindows { get; set; } = true;

    /// <summary>
    /// Focus follows mouse (activate window on hover)
    /// </summary>
    [JsonPropertyName("focusFollowsMouse")]
    public bool FocusFollowsMouse { get; set; }

    /// <summary>
    /// Monitor index to use (-1 = auto/primary)
    /// </summary>
    [JsonPropertyName("monitorIndex")]
    public int MonitorIndex { get; set; } = -1;

    /// <summary>
    /// Use DWM thumbnails for background windows instead of resizing them.
    /// This shows a scaled live preview without actually resizing the game window.
    /// Like JMB's Video FX approach - games stay at their minimum size but
    /// you see scaled thumbnails in the layout positions.
    /// </summary>
    [JsonPropertyName("useThumbnails")]
    public bool UseThumbnails { get; set; } = true;
}
