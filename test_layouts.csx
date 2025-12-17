#r "src/Multiboxer.Core/bin/Debug/net8.0-windows/Multiboxer.Core.dll"
#r "src/Multiboxer.App/bin/Debug/net8.0-windows/Multiboxer.App.dll"

using System;

// Test layout calculations for different screen sizes and slot counts
var screens = new[] {
    (3840, 2100, "4K"),
    (2560, 1400, "1440p"),
    (2560, 1160, "1200p"),
    (1920, 1040, "1080p")
};

var slotCounts = new[] { 6, 12, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72 };

Console.WriteLine("=== HORIZONTAL LAYOUT VERIFICATION ===");
Console.WriteLine($"{"Screen",-8} {"Slots",-6} {"Thumbs/Row",-11} {"Rows",-5} {"ThumbSize",-12} {"MainHeight",-11} {"Status"}");
Console.WriteLine(new string('-', 70));

foreach (var (width, height, name) in screens)
{
    foreach (var slots in slotCounts)
    {
        var (thumbW, thumbH, rows, perRow) = CalculateHorizontal(width, height, slots);
        int mainHeight = height - (thumbH * rows);
        
        int minMain = height >= 2000 ? 1600 : height >= 1400 ? 1100 : height >= 1150 ? 900 : 750;
        string status = mainHeight >= minMain ? "OK" : $"WARN ({mainHeight} < {minMain})";
        
        Console.WriteLine($"{name,-8} {slots,-6} {perRow,-11} {rows,-5} {thumbW}x{thumbH,-7} {mainHeight,-11} {status}");
    }
}

Console.WriteLine("\n=== VERTICAL LAYOUT VERIFICATION ===");
Console.WriteLine($"{"Screen",-8} {"Slots",-6} {"Thumbs/Col",-11} {"Cols",-5} {"ThumbSize",-12} {"MainWidth",-11} {"Status"}");
Console.WriteLine(new string('-', 70));

foreach (var (width, height, name) in screens)
{
    foreach (var slots in slotCounts)
    {
        var (thumbW, thumbH, cols, perCol) = CalculateVertical(width, height, slots);
        int mainWidth = width - (thumbW * cols);
        
        int minMain = width >= 3800 ? 2800 : width >= 2500 ? 1900 : width >= 1900 ? 1400 : (int)(width * 0.7);
        string status = mainWidth >= minMain ? "OK" : $"WARN ({mainWidth} < {minMain})";
        
        Console.WriteLine($"{name,-8} {slots,-6} {perCol,-11} {cols,-5} {thumbW}x{thumbH,-7} {mainWidth,-11} {status}");
    }
}

// Horizontal calculator
(int, int, int, int) CalculateHorizontal(int screenWidth, int screenHeight, int slotCount)
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
        for (int tryRows = 8; tryRows >= 1; tryRows--)
        {
            int perRow = (int)Math.Ceiling((double)slotCount / tryRows);
            int tryThumbWidth = screenWidth / perRow;
            int tryThumbHeight = tryThumbWidth * 9 / 16;
            if (tryThumbWidth >= 100)
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

// Vertical calculator
(int, int, int, int) CalculateVertical(int screenWidth, int screenHeight, int slotCount)
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
        for (int tryCols = 6; tryCols >= 1; tryCols--)
        {
            int perCol = (int)Math.Ceiling((double)slotCount / tryCols);
            int tryThumbHeight = screenHeight / perCol;
            int tryThumbWidth = tryThumbHeight * 16 / 9;
            if (tryThumbHeight >= 60)
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
