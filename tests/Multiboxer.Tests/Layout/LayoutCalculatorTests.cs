using Xunit;

namespace Multiboxer.Tests.Layout;

/// <summary>
/// Tests for layout calculation logic
/// </summary>
public class LayoutCalculatorTests
{
    // Screen configurations to test
    private static readonly (int Width, int Height, string Name, int MinMainH, int MinMainW)[] Screens = new[]
    {
        (3840, 2100, "4K", 1600, 2800),
        (2560, 1400, "1440p", 1100, 1900),
        (2560, 1160, "1200p", 900, 1900),
        (1920, 1040, "1080p", 750, 1400)
    };

    private static readonly int[] SlotCounts = { 6, 12, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72 };

    [Theory]
    [InlineData(3840, 2100, 6)]
    [InlineData(3840, 2100, 12)]
    [InlineData(3840, 2100, 18)]
    [InlineData(3840, 2100, 24)]
    [InlineData(3840, 2100, 30)]
    [InlineData(3840, 2100, 36)]
    [InlineData(3840, 2100, 42)]
    [InlineData(3840, 2100, 48)]
    [InlineData(3840, 2100, 54)]
    [InlineData(3840, 2100, 60)]
    [InlineData(3840, 2100, 66)]
    [InlineData(3840, 2100, 72)]
    [InlineData(2560, 1400, 6)]
    [InlineData(2560, 1400, 12)]
    [InlineData(2560, 1400, 24)]
    [InlineData(2560, 1400, 36)]
    [InlineData(2560, 1400, 72)]
    [InlineData(1920, 1040, 6)]
    [InlineData(1920, 1040, 12)]
    [InlineData(1920, 1040, 24)]
    [InlineData(1920, 1040, 72)]
    public void HorizontalLayout_FitsAllSlots(int screenWidth, int screenHeight, int slotCount)
    {
        var (thumbWidth, thumbHeight, rows, perRow) = CalculateHorizontal(screenWidth, screenHeight, slotCount);

        // Verify all slots fit
        int totalSlots = rows * perRow;
        Assert.True(totalSlots >= slotCount, $"Not enough slots: {totalSlots} < {slotCount}");

        // Verify thumbnails have reasonable size
        Assert.True(thumbWidth >= 100, $"Thumbnail too narrow: {thumbWidth}px");
        Assert.True(thumbHeight >= 50, $"Thumbnail too short: {thumbHeight}px");

        // Verify thumbnails fit on screen
        int totalThumbWidth = thumbWidth * perRow;
        Assert.True(totalThumbWidth <= screenWidth, $"Thumbnails too wide: {totalThumbWidth} > {screenWidth}");

        int totalThumbHeight = thumbHeight * rows;
        Assert.True(totalThumbHeight < screenHeight, $"Thumbnails too tall: {totalThumbHeight} >= {screenHeight}");
    }

    [Theory]
    [InlineData(3840, 2100, 6)]
    [InlineData(3840, 2100, 12)]
    [InlineData(3840, 2100, 18)]
    [InlineData(3840, 2100, 24)]
    [InlineData(3840, 2100, 36)]
    [InlineData(3840, 2100, 72)]
    [InlineData(2560, 1400, 6)]
    [InlineData(2560, 1400, 12)]
    [InlineData(2560, 1400, 36)]
    [InlineData(1920, 1040, 6)]
    [InlineData(1920, 1040, 12)]
    public void VerticalLayout_FitsAllSlots(int screenWidth, int screenHeight, int slotCount)
    {
        var (thumbWidth, thumbHeight, cols, perCol) = CalculateVertical(screenWidth, screenHeight, slotCount);

        // Verify all slots fit
        int totalSlots = cols * perCol;
        Assert.True(totalSlots >= slotCount, $"Not enough slots: {totalSlots} < {slotCount}");

        // Verify thumbnails have reasonable size
        Assert.True(thumbWidth >= 80, $"Thumbnail too narrow: {thumbWidth}px");
        Assert.True(thumbHeight >= 50, $"Thumbnail too short: {thumbHeight}px");

        // Verify thumbnails fit on screen
        int totalThumbWidth = thumbWidth * cols;
        Assert.True(totalThumbWidth < screenWidth, $"Thumbnails too wide: {totalThumbWidth} >= {screenWidth}");

        int totalThumbHeight = thumbHeight * perCol;
        Assert.True(totalThumbHeight <= screenHeight, $"Thumbnails too tall: {totalThumbHeight} > {screenHeight}");
    }

    [Fact]
    public void HorizontalLayout_4K_MaintainsMinMainHeight()
    {
        int screenWidth = 3840, screenHeight = 2100, minMainHeight = 1600;

        foreach (var slotCount in SlotCounts)
        {
            var (thumbWidth, thumbHeight, rows, perRow) = CalculateHorizontal(screenWidth, screenHeight, slotCount);
            int mainHeight = screenHeight - (thumbHeight * rows);

            // For 4K, main should be >= 1600 for reasonable slot counts
            if (slotCount <= 48)
            {
                Assert.True(mainHeight >= minMainHeight,
                    $"4K {slotCount}-box: Main height {mainHeight} < {minMainHeight}");
            }
        }
    }

    [Fact]
    public void VerticalLayout_4K_MaintainsMinMainWidth()
    {
        int screenWidth = 3840, screenHeight = 2100, minMainWidth = 2800;

        foreach (var slotCount in new[] { 6, 12, 18, 24 })
        {
            var (thumbWidth, thumbHeight, cols, perCol) = CalculateVertical(screenWidth, screenHeight, slotCount);
            int mainWidth = screenWidth - (thumbWidth * cols);

            Assert.True(mainWidth >= minMainWidth,
                $"4K {slotCount}-box vertical: Main width {mainWidth} < {minMainWidth}");
        }
    }

    // Horizontal calculator (same logic as in WindowLayoutManagerDialog)
    private (int thumbWidth, int thumbHeight, int rows, int perRow) CalculateHorizontal(
        int screenWidth, int screenHeight, int slotCount)
    {
        int minMainHeight = screenHeight >= 2000 ? 1600 : screenHeight >= 1400 ? 1100 : screenHeight >= 1150 ? 900 : 750;
        int minThumbWidth = screenWidth >= 3800 ? 200 : screenWidth >= 2500 ? 180 : screenWidth >= 1900 ? 160 : 140;

        int bestRows = 1, bestPerRow = slotCount, bestThumbHeight = 0;

        for (int tryRows = 1; tryRows <= 8; tryRows++)
        {
            int perRow = (int)Math.Ceiling((double)slotCount / tryRows);
            int tryThumbWidth = screenWidth / perRow;
            if (tryThumbWidth < minThumbWidth) continue;

            int tryThumbHeight = tryThumbWidth * 9 / 16;
            int totalThumbHeight = tryThumbHeight * tryRows;
            int mainHeight = screenHeight - totalThumbHeight;

            if (mainHeight >= minMainHeight && tryThumbHeight > bestThumbHeight)
            {
                bestRows = tryRows;
                bestPerRow = perRow;
                bestThumbHeight = tryThumbHeight;
            }
        }

        if (bestThumbHeight == 0)
        {
            // Fallback: find configuration that fits within screen, sacrificing main height
            for (int tryRows = 1; tryRows <= 12; tryRows++)
            {
                int perRow = (int)Math.Ceiling((double)slotCount / tryRows);
                int tryThumbWidth = screenWidth / perRow;
                int tryThumbHeight = tryThumbWidth * 9 / 16;
                int totalThumbHeight = tryThumbHeight * tryRows;

                // Must fit within screen and have reasonable thumbnail size
                if (tryThumbWidth >= 100 && totalThumbHeight < screenHeight)
                {
                    bestRows = tryRows;
                    bestPerRow = perRow;
                    bestThumbHeight = tryThumbHeight;
                    break;
                }
            }
        }

        int actualRows = (int)Math.Ceiling((double)slotCount / bestPerRow);
        if (actualRows > bestRows) bestRows = actualRows;

        int thumbWidth = screenWidth / bestPerRow;
        int thumbHeight = thumbWidth * 9 / 16;
        return (thumbWidth, thumbHeight, bestRows, bestPerRow);
    }

    // Vertical calculator (same logic as in WindowLayoutManagerDialog)
    private (int thumbWidth, int thumbHeight, int cols, int perCol) CalculateVertical(
        int screenWidth, int screenHeight, int slotCount)
    {
        int minMainWidth = screenWidth >= 3800 ? 2800 : screenWidth >= 2500 ? 1900 : screenWidth >= 1900 ? 1400 : (int)(screenWidth * 0.7);
        int minThumbHeight = screenHeight >= 2000 ? 120 : screenHeight >= 1400 ? 100 : screenHeight >= 1150 ? 90 : 80;

        int bestCols = 1, bestPerCol = slotCount, bestThumbWidth = 0;

        for (int tryCols = 1; tryCols <= 6; tryCols++)
        {
            int perCol = (int)Math.Ceiling((double)slotCount / tryCols);
            int tryThumbHeight = screenHeight / perCol;
            if (tryThumbHeight < minThumbHeight) continue;

            int tryThumbWidth = tryThumbHeight * 16 / 9;
            int totalThumbWidth = tryThumbWidth * tryCols;
            int mainWidth = screenWidth - totalThumbWidth;

            if (mainWidth >= minMainWidth && tryThumbWidth > bestThumbWidth)
            {
                bestCols = tryCols;
                bestPerCol = perCol;
                bestThumbWidth = tryThumbWidth;
            }
        }

        if (bestThumbWidth == 0)
        {
            // Fallback: find configuration that fits within screen, sacrificing main width
            for (int tryCols = 1; tryCols <= 8; tryCols++)
            {
                int perCol = (int)Math.Ceiling((double)slotCount / tryCols);
                int tryThumbHeight = screenHeight / perCol;
                int tryThumbWidth = tryThumbHeight * 16 / 9;
                int totalThumbWidth = tryThumbWidth * tryCols;

                // Must fit within screen and have reasonable thumbnail size
                if (tryThumbHeight >= 60 && totalThumbWidth < screenWidth)
                {
                    bestCols = tryCols;
                    bestPerCol = perCol;
                    bestThumbWidth = tryThumbWidth;
                    break;
                }
            }
        }

        int actualCols = (int)Math.Ceiling((double)slotCount / bestPerCol);
        if (actualCols > bestCols) bestCols = actualCols;

        int thumbHeight = screenHeight / bestPerCol;
        int thumbWidth = thumbHeight * 16 / 9;
        return (thumbWidth, thumbHeight, bestCols, bestPerCol);
    }
}
