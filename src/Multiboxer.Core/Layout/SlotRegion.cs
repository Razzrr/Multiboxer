using System.Text.Json.Serialization;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Represents the foreground and background regions for a slot.
/// ISBoxer-style: each slot has a "home" position when in background,
/// and swaps to the main region when focused.
/// </summary>
public class SlotRegion
{
    /// <summary>
    /// Slot ID this region belongs to
    /// </summary>
    [JsonPropertyName("slotId")]
    public int SlotId { get; set; }

    /// <summary>
    /// Region when this slot is the active/focused window (large, main area)
    /// </summary>
    [JsonPropertyName("foreRegion")]
    public WindowRect ForeRegion { get; set; } = new();

    /// <summary>
    /// Region when this slot is a background window (small thumbnail)
    /// </summary>
    [JsonPropertyName("backRegion")]
    public WindowRect BackRegion { get; set; } = new();

    /// <summary>
    /// Whether this slot is currently in the foreground position
    /// </summary>
    [JsonIgnore]
    public bool IsInForeground { get; set; }
}

/// <summary>
/// Simple rectangle for window positioning (absolute pixels)
/// </summary>
public class WindowRect
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonIgnore]
    public bool IsValid => Width > 0 && Height > 0;

    public WindowRect() { }

    public WindowRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public override string ToString() => $"({X}, {Y}, {Width}x{Height})";
}
