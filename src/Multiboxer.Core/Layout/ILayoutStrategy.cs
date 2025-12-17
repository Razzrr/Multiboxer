using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Interface for window layout strategies
/// </summary>
public interface ILayoutStrategy
{
    /// <summary>
    /// Name of the layout strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of the layout
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Apply the layout to the given slots
    /// </summary>
    /// <param name="slots">Slots to arrange (first slot is the main/foreground)</param>
    /// <param name="monitor">Monitor to arrange windows on</param>
    /// <param name="options">Layout options</param>
    void Apply(IReadOnlyList<Slot> slots, MonitorInfo monitor, LayoutOptions options);

    /// <summary>
    /// Calculate window positions without applying them
    /// </summary>
    /// <param name="slotCount">Number of slots to arrange</param>
    /// <param name="monitor">Monitor to arrange windows on</param>
    /// <param name="options">Layout options</param>
    /// <returns>List of window regions for each slot index</returns>
    IReadOnlyList<WindowRegion> CalculateRegions(int slotCount, MonitorInfo monitor, LayoutOptions options);
}
