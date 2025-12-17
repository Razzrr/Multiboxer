using System.Text.Json.Serialization;
using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Custom layout with user-defined regions
/// </summary>
public class CustomLayout : ILayoutStrategy
{
    /// <summary>
    /// Name of this custom layout
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Custom";

    [JsonIgnore]
    public string Description => $"Custom layout: {Name}";

    /// <summary>
    /// The main (foreground) window region
    /// </summary>
    [JsonPropertyName("mainRegion")]
    public WindowRegion MainRegion { get; set; } = WindowRegion.FullScreen;

    /// <summary>
    /// Regions for background windows (index 0 = first background slot)
    /// </summary>
    [JsonPropertyName("regions")]
    public List<WindowRegion> Regions { get; set; } = new();

    public void Apply(IReadOnlyList<Slot> slots, MonitorInfo monitor, LayoutOptions options)
    {
        if (slots.Count == 0)
            return;

        var bounds = options.AvoidTaskbar ? monitor.WorkingArea : monitor.Bounds;

        // Apply main region to first slot
        var mainSlot = slots[0];
        if (mainSlot.MainWindowHandle != IntPtr.Zero)
        {
            var (x, y, width, height) = MainRegion.GetAbsoluteValues(bounds);

            if (options.MakeBorderless)
            {
                WindowHelper.MakeBorderless(mainSlot.MainWindowHandle);
            }

            if (options.RescaleWindows)
            {
                WindowHelper.SetWindowPositionDeferred(mainSlot.MainWindowHandle, x, y, width, height);
            }
            else
            {
                WindowHelper.SetWindowPosition(mainSlot.MainWindowHandle, x, y, width, height);
            }
            mainSlot.UpdateWindowInfo();
        }

        // Apply other regions
        for (int i = 1; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.MainWindowHandle == IntPtr.Zero)
                continue;

            // Use modulo to cycle through regions if we have more slots than regions
            var regionIndex = (i - 1) % Math.Max(1, Regions.Count);

            if (regionIndex < Regions.Count)
            {
                var region = Regions[regionIndex];
                var (x, y, width, height) = region.GetAbsoluteValues(bounds);

                if (options.MakeBorderless)
                {
                    WindowHelper.MakeBorderless(slot.MainWindowHandle);
                }

                if (options.RescaleWindows)
                {
                    WindowHelper.SetWindowPositionDeferred(slot.MainWindowHandle, x, y, width, height);
                }
                else
                {
                    WindowHelper.SetWindowPosition(slot.MainWindowHandle, x, y, width, height);
                }
                slot.UpdateWindowInfo();
            }
        }
    }

    public IReadOnlyList<WindowRegion> CalculateRegions(int slotCount, MonitorInfo monitor, LayoutOptions options)
    {
        var result = new List<WindowRegion>();

        if (slotCount == 0)
            return result;

        // Main region first
        result.Add(MainRegion);

        // Then the custom regions
        for (int i = 1; i < slotCount; i++)
        {
            var regionIndex = (i - 1) % Math.Max(1, Regions.Count);
            if (regionIndex < Regions.Count)
            {
                result.Add(Regions[regionIndex]);
            }
            else
            {
                // Default to a small region if no custom region defined
                result.Add(new WindowRegion
                {
                    X = 0,
                    Y = 80,
                    Width = 20,
                    Height = 20,
                    UsePercentage = true
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Create a common "PiP" (picture-in-picture) style layout
    /// </summary>
    public static CustomLayout CreatePiPLayout(string name, int pipCount = 3)
    {
        var layout = new CustomLayout
        {
            Name = name,
            MainRegion = new WindowRegion
            {
                X = 0,
                Y = 0,
                Width = 100,
                Height = 100,
                UsePercentage = true
            }
        };

        // Create small PiP windows in bottom-right corner
        int pipWidth = 20;
        int pipHeight = 20;

        for (int i = 0; i < pipCount; i++)
        {
            layout.Regions.Add(new WindowRegion
            {
                X = 100 - pipWidth * (i + 1),
                Y = 100 - pipHeight,
                Width = pipWidth,
                Height = pipHeight,
                UsePercentage = true
            });
        }

        return layout;
    }

    /// <summary>
    /// Create a grid layout
    /// </summary>
    public static CustomLayout CreateGridLayout(string name, int columns, int rows)
    {
        var layout = new CustomLayout { Name = name };

        int cellWidth = 100 / columns;
        int cellHeight = 100 / rows;

        // First cell is main region
        layout.MainRegion = new WindowRegion
        {
            X = 0,
            Y = 0,
            Width = cellWidth,
            Height = cellHeight,
            UsePercentage = true
        };

        // Rest are background regions
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                if (row == 0 && col == 0)
                    continue; // Skip main region

                layout.Regions.Add(new WindowRegion
                {
                    X = col * cellWidth,
                    Y = row * cellHeight,
                    Width = cellWidth,
                    Height = cellHeight,
                    UsePercentage = true
                });
            }
        }

        return layout;
    }
}
