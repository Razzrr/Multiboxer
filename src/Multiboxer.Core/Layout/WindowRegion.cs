using System.Text.Json.Serialization;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Defines a region on the screen where a window should be placed
/// </summary>
public class WindowRegion
{
    /// <summary>
    /// X position (absolute or percentage based on UsePercentage)
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y position (absolute or percentage based on UsePercentage)
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Width (absolute or percentage based on UsePercentage)
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Height (absolute or percentage based on UsePercentage)
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// Whether values are percentages (0-100) instead of pixels
    /// </summary>
    [JsonPropertyName("usePercentage")]
    public bool UsePercentage { get; set; }

    /// <summary>
    /// Index of the monitor to use (0 = primary, -1 = auto)
    /// </summary>
    [JsonPropertyName("monitor")]
    public int MonitorIndex { get; set; } = -1;

    /// <summary>
    /// Calculate absolute pixel values based on a monitor's bounds
    /// </summary>
    public (int X, int Y, int Width, int Height) GetAbsoluteValues(Window.Rectangle monitorBounds)
    {
        if (UsePercentage)
        {
            return (
                monitorBounds.X + (monitorBounds.Width * X / 100),
                monitorBounds.Y + (monitorBounds.Height * Y / 100),
                monitorBounds.Width * Width / 100,
                monitorBounds.Height * Height / 100
            );
        }
        else
        {
            return (
                monitorBounds.X + X,
                monitorBounds.Y + Y,
                Width,
                Height
            );
        }
    }

    /// <summary>
    /// Create a region for the full monitor
    /// </summary>
    public static WindowRegion FullScreen => new()
    {
        X = 0,
        Y = 0,
        Width = 100,
        Height = 100,
        UsePercentage = true
    };
}
