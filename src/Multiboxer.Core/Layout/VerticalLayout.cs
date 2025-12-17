using Multiboxer.Core.Slots;
using Multiboxer.Core.Window;

namespace Multiboxer.Core.Layout;

/// <summary>
/// Vertical layout - main window large, thumbnails in a strip on the right
/// </summary>
public class VerticalLayout : ILayoutStrategy
{
    // Thumbnail size - small enough to be unobtrusive
    private const int ThumbnailWidth = 200;
    private const int ThumbnailHeight = 150;

    public string Name => "Vertical";
    public string Description => "Main window large, thumbnail strip on right";

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
        int thumbnailStripWidth = thumbnailCount > 0 ? ThumbnailWidth : 0;
        int mainWidth = bounds.Width - thumbnailStripWidth;

        // Apply main window (slot 0) - takes most of the screen
        if (slots.Count > 0 && slots[0].MainWindowHandle != IntPtr.Zero)
        {
            WindowHelper.SetWindowPosition(
                slots[0].MainWindowHandle,
                bounds.X,
                bounds.Y,
                mainWidth,
                bounds.Height);
        }

        // Apply thumbnail windows - small strip on right
        if (thumbnailCount > 0)
        {
            int thumbnailX = bounds.X + mainWidth;
            int currentY = bounds.Y;

            for (int i = 1; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.MainWindowHandle == IntPtr.Zero)
                    continue;

                WindowHelper.SetWindowPosition(
                    slot.MainWindowHandle,
                    thumbnailX,
                    currentY,
                    ThumbnailWidth,
                    ThumbnailHeight);

                currentY += ThumbnailHeight;
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
        int mainWidth = bounds.Width - ThumbnailWidth;

        // Main window region
        regions.Add(new WindowRegion
        {
            X = 0,
            Y = 0,
            Width = mainWidth,
            Height = bounds.Height,
            UsePercentage = false
        });

        // Thumbnail regions
        int currentY = 0;
        for (int i = 0; i < thumbnailCount; i++)
        {
            regions.Add(new WindowRegion
            {
                X = mainWidth,
                Y = currentY,
                Width = ThumbnailWidth,
                Height = ThumbnailHeight,
                UsePercentage = false
            });
            currentY += ThumbnailHeight;
        }

        return regions;
    }
}
