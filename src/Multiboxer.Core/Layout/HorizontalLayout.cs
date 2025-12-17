using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Horizontal layout - main window large, thumbnails in a strip at bottom
/// </summary>
public class HorizontalLayout : ILayoutStrategy
{
    // Thumbnail size - small enough to be unobtrusive
    private const int ThumbnailWidth = 200;
    private const int ThumbnailHeight = 150;

    public string Name => "Horizontal";
    public string Description => "Main window large, thumbnail strip at bottom";

    public void Apply(IReadOnlyList<Slot> slots, MonitorInfo monitor, LayoutOptions options)
    {
        if (slots.Count == 0)
            return;

        var bounds = options.AvoidTaskbar ? monitor.WorkingArea : monitor.Bounds;

        // First pass: make windows borderless if needed
        if (options.MakeBorderless)
        {
            foreach (var slot in slots)
            {
                if (slot.MainWindowHandle != IntPtr.Zero)
                {
                    WindowHelper.MakeBorderless(slot.MainWindowHandle);
                }
            }
        }

        // Calculate positions
        int thumbnailCount = slots.Count - 1;
        int thumbnailStripHeight = thumbnailCount > 0 ? ThumbnailHeight : 0;
        int mainHeight = bounds.Height - thumbnailStripHeight;

        // Apply main window (slot 0) - takes most of the screen
        if (slots.Count > 0 && slots[0].MainWindowHandle != IntPtr.Zero)
        {
            WindowHelper.SetWindowPosition(
                slots[0].MainWindowHandle,
                bounds.X,
                bounds.Y,
                bounds.Width,
                mainHeight);
        }

        // Apply thumbnail windows - small strip at bottom
        if (thumbnailCount > 0)
        {
            int thumbnailY = bounds.Y + mainHeight;
            int currentX = bounds.X;

            for (int i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.MainWindowHandle == IntPtr.Zero)
                    continue;

                WindowHelper.SetWindowPosition(
                    slot.MainWindowHandle,
                    currentX,
                    thumbnailY,
                    ThumbnailWidth,
                    ThumbnailHeight);

                currentX += ThumbnailWidth;
            }
        }

        // Update slot window info
        foreach (var slot in slots)
        {
            slot.UpdateWindowInfo();
        }
    }

    public IReadOnlyList<WindowRegion> CalculateRegions(int slotCount, MonitorInfo monitor, LayoutOptions options)
    {
        var regions = new List<WindowRegion>();
        var bounds = options.AvoidTaskbar ? monitor.WorkingArea : monitor.Bounds;

        if (slotCount == 0)
            return regions;

        // Single slot - full screen
        if (slotCount == 1)
        {
            regions.Add(new WindowRegion
            {
                X = 0,
                Y = 0,
                Width = bounds.Width,
                Height = bounds.Height,
                UsePercentage = false
            });
            return regions;
        }

        int thumbnailCount = slotCount - 1;
        int mainHeight = bounds.Height - ThumbnailHeight;

        // Main window region
        regions.Add(new WindowRegion
        {
            X = 0,
            Y = 0,
            Width = bounds.Width,
            Height = mainHeight,
            UsePercentage = false
        });

        // Thumbnail regions
        int currentX = 0;
        for (int i = 0; i < thumbnailCount; i++)
        {
            regions.Add(new WindowRegion
            {
                X = currentX,
                Y = mainHeight,
                Width = ThumbnailWidth,
                Height = ThumbnailHeight,
                UsePercentage = false
            });
            currentX += ThumbnailWidth;
        }

        return regions;
    }
}
