using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Multiboxer.Core.Config;
using Multiboxer.Core.Layout;
using Multiboxer.Core.Window;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using MonitorRect = Multiboxer.Core.Window.Rectangle;

namespace Multiboxer.App.Views;

/// <summary>
/// View model for a slot region in the editor
/// </summary>
public class SlotRegionViewModel : INotifyPropertyChanged
{
    private int _slotId;
    private int _foreX, _foreY, _foreWidth, _foreHeight;
    private int _backX, _backY, _backWidth, _backHeight;

    public int SlotId
    {
        get => _slotId;
        set { _slotId = value; OnPropertyChanged(nameof(SlotId)); OnPropertyChanged(nameof(SlotLabel)); }
    }

    public string SlotLabel => $"Slot {SlotId}";

    public int ForeX { get => _foreX; set { _foreX = value; OnPropertyChanged(nameof(ForeX)); } }
    public int ForeY { get => _foreY; set { _foreY = value; OnPropertyChanged(nameof(ForeY)); } }
    public int ForeWidth { get => _foreWidth; set { _foreWidth = value; OnPropertyChanged(nameof(ForeWidth)); } }
    public int ForeHeight { get => _foreHeight; set { _foreHeight = value; OnPropertyChanged(nameof(ForeHeight)); } }

    public int BackX { get => _backX; set { _backX = value; OnPropertyChanged(nameof(BackX)); } }
    public int BackY { get => _backY; set { _backY = value; OnPropertyChanged(nameof(BackY)); } }
    public int BackWidth { get => _backWidth; set { _backWidth = value; OnPropertyChanged(nameof(BackWidth)); } }
    public int BackHeight { get => _backHeight; set { _backHeight = value; OnPropertyChanged(nameof(BackHeight)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public SlotRegion ToSlotRegion()
    {
        return new SlotRegion
        {
            SlotId = SlotId,
            ForeRegion = new WindowRect(ForeX, ForeY, ForeWidth, ForeHeight),
            BackRegion = new WindowRect(BackX, BackY, BackWidth, BackHeight)
        };
    }

    public static SlotRegionViewModel FromSlotRegion(SlotRegion region)
    {
        return new SlotRegionViewModel
        {
            SlotId = region.SlotId,
            ForeX = region.ForeRegion.X,
            ForeY = region.ForeRegion.Y,
            ForeWidth = region.ForeRegion.Width,
            ForeHeight = region.ForeRegion.Height,
            BackX = region.BackRegion.X,
            BackY = region.BackRegion.Y,
            BackWidth = region.BackRegion.Width,
            BackHeight = region.BackRegion.Height
        };
    }
}

public partial class WindowLayoutManagerDialog : Window
{
    private ObservableCollection<SlotRegionViewModel> _slotRegions = new();
    private ObservableCollection<string> _layoutNames = new();
    private List<SavedWindowLayout> _savedLayouts = new();
    private MonitorRect _monitorBounds;

    public WindowLayoutManagerDialog()
    {
        InitializeComponent();
        SlotRegionsItemsControl.ItemsSource = _slotRegions;
        LayoutsListBox.ItemsSource = _layoutNames;

        // Get monitor bounds for preview
        var monitor = MonitorManager.GetPrimaryMonitor();
        _monitorBounds = monitor?.WorkingArea ?? new MonitorRect(0, 0, 1920, 1080);

        // Show screen info
        ScreenInfoText.Text = $"Screen: {_monitorBounds.Width}x{_monitorBounds.Height}";

        // Set default main region size to screen size
        MainWidthTextBox.Text = _monitorBounds.Width.ToString();
        MainHeightTextBox.Text = _monitorBounds.Height.ToString();

        // Subscribe to collection changes to update preview
        _slotRegions.CollectionChanged += (s, e) => UpdatePreview();

        LoadSavedLayouts();

        // Update preview when canvas is loaded
        PreviewCanvas.Loaded += (s, e) => UpdatePreview();
        PreviewCanvas.SizeChanged += (s, e) => UpdatePreview();
    }

    private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Update UI hints based on style
        if (ThumbnailRowsComboBox == null) return;

        var style = (StyleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        if (style.StartsWith("Vertical"))
        {
            // For vertical, "Rows" becomes "Columns"
            // (we could rename the label dynamically but for simplicity just use it as columns)
        }
    }

    private void LoadSavedLayouts()
    {
        _savedLayouts.Clear();
        _layoutNames.Clear();

        // Load from config
        if (App.ConfigManager.Settings.SavedWindowLayouts != null)
        {
            foreach (var layout in App.ConfigManager.Settings.SavedWindowLayouts)
            {
                _savedLayouts.Add(layout);
                _layoutNames.Add(layout.Name);
            }
        }

        // Add default layouts if none exist
        if (_savedLayouts.Count == 0)
        {
            var defaultCounts = new[] { 6, 12, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72 };
            foreach (var count in defaultCounts)
            {
                // Create horizontal layout
                var horizontalLayout = CreateDefaultLayout($"{count}-Box Horizontal", count, isVertical: false);
                _savedLayouts.Add(horizontalLayout);
                _layoutNames.Add(horizontalLayout.Name);

                // Create vertical layout
                var verticalLayout = CreateDefaultLayout($"{count}-Box Vertical", count, isVertical: true);
                _savedLayouts.Add(verticalLayout);
                _layoutNames.Add(verticalLayout.Name);
            }

            // Save to config
            App.ConfigManager.Settings.SavedWindowLayouts = _savedLayouts;
            App.ConfigManager.Save();
        }

        if (_layoutNames.Count > 0)
        {
            LayoutsListBox.SelectedIndex = 0;
        }
    }

    private SavedWindowLayout CreateDefaultLayout(string name, int slotCount = 6, bool isVertical = false)
    {
        var monitor = MonitorManager.GetPrimaryMonitor();
        var bounds = monitor?.WorkingArea ?? new MonitorRect(0, 0, 1920, 1080);

        var layout = new SavedWindowLayout { Name = name };

        if (isVertical)
        {
            // Vertical layout: thumbnails on right side
            var (thumbnailWidth, thumbnailHeight, columns, thumbsPerColumn) =
                CalculateOptimalVerticalThumbnailLayout(bounds.Width, bounds.Height, slotCount);

            int thumbnailStripWidth = thumbnailWidth * columns;
            int mainWidth = bounds.Width - thumbnailStripWidth;

            for (int i = 1; i <= slotCount; i++)
            {
                int thumbnailIndex = i - 1;
                int col = thumbnailIndex / thumbsPerColumn;
                int row = thumbnailIndex % thumbsPerColumn;

                layout.SlotRegions.Add(new SlotRegion
                {
                    SlotId = i,
                    ForeRegion = new WindowRect(bounds.X, bounds.Y, mainWidth, bounds.Height),
                    BackRegion = new WindowRect(
                        bounds.X + mainWidth + col * thumbnailWidth,
                        bounds.Y + row * thumbnailHeight,
                        thumbnailWidth,
                        thumbnailHeight)
                });
            }
        }
        else
        {
            // Horizontal layout: thumbnails at bottom
            var (thumbnailWidth, thumbnailHeight, thumbnailRows, thumbsPerRow) =
                CalculateOptimalThumbnailLayout(bounds.Width, bounds.Height, slotCount);

            int thumbnailStripHeight = thumbnailHeight * thumbnailRows;
            int mainHeight = bounds.Height - thumbnailStripHeight;

            for (int i = 1; i <= slotCount; i++)
            {
                int thumbnailIndex = i - 1;
                int row = thumbnailIndex / thumbsPerRow;
                int col = thumbnailIndex % thumbsPerRow;

                layout.SlotRegions.Add(new SlotRegion
                {
                    SlotId = i,
                    ForeRegion = new WindowRect(bounds.X, bounds.Y, bounds.Width, mainHeight),
                    BackRegion = new WindowRect(
                        bounds.X + col * thumbnailWidth,
                        bounds.Y + mainHeight + row * thumbnailHeight,
                        thumbnailWidth,
                        thumbnailHeight)
                });
            }
        }

        return layout;
    }

    /// <summary>
    /// Calculate optimal thumbnail size and row count to maximize main window while fitting all thumbnails.
    /// Supports multiple resolutions: 4K, 1440p, 1200p, 1080p
    /// Uses 16:9 aspect ratio for thumbnails.
    /// </summary>
    private (int width, int height, int rows, int perRow) CalculateOptimalThumbnailLayout(
        int screenWidth, int screenHeight, int slotCount)
    {
        // Determine minimum main window height based on screen resolution
        // 4K (2160p): main >= 1600
        // 1440p: main >= 1100
        // 1200p: main >= 900
        // 1080p: main >= 750
        int minMainHeight = screenHeight switch
        {
            >= 2000 => 1600,  // 4K
            >= 1400 => 1100,  // 1440p
            >= 1150 => 900,   // 1200p
            _ => 750          // 1080p and below
        };

        // Calculate maximum space available for thumbnails
        int maxThumbnailAreaHeight = screenHeight - minMainHeight;

        // Minimum readable thumbnail width based on screen width
        int minThumbWidth = screenWidth switch
        {
            >= 3800 => 200,   // 4K - can go smaller
            >= 2500 => 180,   // 1440p
            >= 1900 => 160,   // 1200p/1080p
            _ => 140          // Smaller screens
        };

        // Calculate optimal layout
        // Strategy: Find the arrangement that keeps main window >= minMainHeight
        // while making thumbnails as large as possible

        int thumbsPerRow, rows, thumbWidth, thumbHeight;

        // Try different row counts and find the best fit
        int bestRows = 1;
        int bestPerRow = slotCount;
        int bestThumbHeight = 0;

        for (int tryRows = 1; tryRows <= 8; tryRows++)
        {
            int perRow = (int)Math.Ceiling((double)slotCount / tryRows);
            int tryThumbWidth = screenWidth / perRow;

            // Skip if thumbnails too small
            if (tryThumbWidth < minThumbWidth)
                continue;

            int tryThumbHeight = tryThumbWidth * 9 / 16; // 16:9 aspect ratio
            int totalThumbHeight = tryThumbHeight * tryRows;

            // Check if main window meets minimum height requirement
            int mainHeight = screenHeight - totalThumbHeight;
            if (mainHeight >= minMainHeight)
            {
                // This configuration works - check if it's better than current best
                // Prefer larger thumbnails (fewer rows) when main height requirement is met
                if (tryThumbHeight > bestThumbHeight)
                {
                    bestRows = tryRows;
                    bestPerRow = perRow;
                    bestThumbHeight = tryThumbHeight;
                }
            }
        }

        // If no configuration met the minimum, find one that fits within screen
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

        thumbsPerRow = bestPerRow;
        rows = bestRows;
        thumbWidth = screenWidth / thumbsPerRow;
        thumbHeight = thumbWidth * 9 / 16;

        // Ensure we have enough rows for all slots
        int actualRows = (int)Math.Ceiling((double)slotCount / thumbsPerRow);
        if (actualRows > rows)
            rows = actualRows;

        return (thumbWidth, thumbHeight, rows, thumbsPerRow);
    }

    /// <summary>
    /// Calculate optimal vertical thumbnail layout (thumbnails on right side).
    /// Supports multiple resolutions: 4K, 1440p, 1200p, 1080p
    /// Uses 16:9 aspect ratio for thumbnails.
    /// </summary>
    private (int width, int height, int columns, int perColumn) CalculateOptimalVerticalThumbnailLayout(
        int screenWidth, int screenHeight, int slotCount)
    {
        // Determine minimum main window width based on screen resolution
        // 4K (3840): main >= 2800 (73% of width)
        // 1440p (2560): main >= 1900 (74% of width)
        // 1200p (2560): main >= 1900
        // 1080p (1920): main >= 1400 (73% of width)
        int minMainWidth = screenWidth switch
        {
            >= 3800 => 2800,  // 4K
            >= 2500 => 1900,  // 1440p / 1200p
            >= 1900 => 1400,  // 1080p
            _ => (int)(screenWidth * 0.7)  // 70% for smaller
        };

        // Calculate maximum space available for thumbnail columns
        int maxThumbnailAreaWidth = screenWidth - minMainWidth;

        // Minimum readable thumbnail height based on screen height
        int minThumbHeight = screenHeight switch
        {
            >= 2000 => 120,   // 4K
            >= 1400 => 100,   // 1440p
            >= 1150 => 90,    // 1200p
            _ => 80           // 1080p and below
        };

        int thumbsPerColumn, columns, thumbWidth, thumbHeight;

        // Try different column counts and find the best fit
        int bestColumns = 1;
        int bestPerColumn = slotCount;
        int bestThumbWidth = 0;

        for (int tryCols = 1; tryCols <= 6; tryCols++)
        {
            int perCol = (int)Math.Ceiling((double)slotCount / tryCols);
            int tryThumbHeight = screenHeight / perCol;

            // Skip if thumbnails too small
            if (tryThumbHeight < minThumbHeight)
                continue;

            int tryThumbWidth = tryThumbHeight * 16 / 9; // 16:9 aspect ratio
            int totalThumbWidth = tryThumbWidth * tryCols;

            // Check if main window meets minimum width requirement
            int mainWidth = screenWidth - totalThumbWidth;
            if (mainWidth >= minMainWidth)
            {
                // This configuration works - check if it's better than current best
                // Prefer larger thumbnails (fewer columns) when main width requirement is met
                if (tryThumbWidth > bestThumbWidth)
                {
                    bestColumns = tryCols;
                    bestPerColumn = perCol;
                    bestThumbWidth = tryThumbWidth;
                }
            }
        }

        // If no configuration met the minimum, find one that fits within screen
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
                    bestColumns = tryCols;
                    bestPerColumn = perCol;
                    bestThumbWidth = tryThumbWidth;
                    break;
                }
            }
        }

        thumbsPerColumn = bestPerColumn;
        columns = bestColumns;
        thumbHeight = screenHeight / thumbsPerColumn;
        thumbWidth = thumbHeight * 16 / 9;

        // Ensure we have enough columns for all slots
        int actualCols = (int)Math.Ceiling((double)slotCount / thumbsPerColumn);
        if (actualCols > columns)
            columns = actualCols;

        return (thumbWidth, thumbHeight, columns, thumbsPerColumn);
    }

    private void LayoutsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutsListBox.SelectedIndex < 0 || LayoutsListBox.SelectedIndex >= _savedLayouts.Count)
            return;

        var layout = _savedLayouts[LayoutsListBox.SelectedIndex];
        LoadLayoutIntoEditor(layout);
    }

    private void LoadLayoutIntoEditor(SavedWindowLayout layout)
    {
        LayoutNameTextBox.Text = layout.Name;
        _slotRegions.Clear();

        foreach (var region in layout.SlotRegions)
        {
            _slotRegions.Add(SlotRegionViewModel.FromSlotRegion(region));
        }

        UpdatePreview();
    }

    private void NewLayout_Click(object sender, RoutedEventArgs e)
    {
        var newLayout = CreateDefaultLayout($"Layout {_savedLayouts.Count + 1}");
        _savedLayouts.Add(newLayout);
        _layoutNames.Add(newLayout.Name);
        LayoutsListBox.SelectedIndex = _savedLayouts.Count - 1;
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if (LayoutsListBox.SelectedIndex < 0)
            return;

        var result = MessageBox.Show("Delete this layout?", "Confirm Delete", MessageBoxButton.YesNo);
        if (result != MessageBoxResult.Yes)
            return;

        int index = LayoutsListBox.SelectedIndex;
        _savedLayouts.RemoveAt(index);
        _layoutNames.RemoveAt(index);

        // Save changes to config
        App.ConfigManager.Settings.SavedWindowLayouts = _savedLayouts;
        App.ConfigManager.Save();

        if (_layoutNames.Count > 0)
            LayoutsListBox.SelectedIndex = Math.Min(index, _layoutNames.Count - 1);
        else
            _slotRegions.Clear(); // Clear editor if no layouts left
    }

    private void GenerateLayout_Click(object sender, RoutedEventArgs e)
    {
        var monitor = MonitorManager.GetPrimaryMonitor();
        var bounds = monitor?.WorkingArea ?? new MonitorRect(0, 0, 1920, 1080);

        int slotCount = GetSelectedSlotCount();
        string style = (StyleComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Horizontal";

        // Get aspect ratio
        double aspectRatio = GetSelectedAspectRatio();

        // Get gap between thumbnails
        int gap = int.TryParse(GapTextBox.Text, out int g) ? g : 0;

        // Get number of rows (or columns for vertical)
        int thumbnailRows = ThumbnailRowsComboBox.SelectedIndex + 1;

        _slotRegions.Clear();

        if (style.StartsWith("Horizontal"))
        {
            GenerateHorizontalLayout(bounds, slotCount, thumbnailRows, aspectRatio, gap);
        }
        else if (style.StartsWith("Vertical"))
        {
            GenerateVerticalLayout(bounds, slotCount, thumbnailRows, aspectRatio, gap);
        }
        else if (style.StartsWith("Grid"))
        {
            GenerateGridLayout(bounds, slotCount, gap);
        }
        else if (style.StartsWith("Stacked"))
        {
            GenerateStackedLayout(bounds, slotCount);
        }

        UpdatePreview();
    }

    private double GetSelectedAspectRatio()
    {
        var ratio = (AspectRatioComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "16:9";
        return ratio switch
        {
            "16:9" => 16.0 / 9.0,
            "16:10" => 16.0 / 10.0,
            "4:3" => 4.0 / 3.0,
            "21:9" => 21.0 / 9.0,
            _ => 16.0 / 9.0
        };
    }

    private void GenerateHorizontalLayout(MonitorRect bounds, int slotCount, int rows, double aspectRatio, int gap)
    {
        // Horizontal: Main window on top, thumbnails at bottom in rows
        int thumbnailWidth, thumbnailHeight;

        if (ThumbAutoRadio.IsChecked == true)
        {
            // Auto-calculate: fit all thumbnails across the screen width in the specified rows
            int thumbsPerRow = (int)Math.Ceiling((double)slotCount / rows);
            thumbnailWidth = (bounds.Width - (thumbsPerRow - 1) * gap) / thumbsPerRow;
            thumbnailHeight = (int)(thumbnailWidth / aspectRatio);
        }
        else
        {
            // Use custom sizes
            thumbnailWidth = int.TryParse(ThumbnailWidthTextBox.Text, out int tw) ? tw : 320;
            thumbnailHeight = int.TryParse(ThumbnailHeightTextBox.Text, out int th) ? th : 180;
        }

        int totalThumbnailHeight = thumbnailHeight * rows + gap * (rows - 1);

        // Main region size
        int mainWidth, mainHeight;
        if (MainAutoRadio.IsChecked == true)
        {
            mainWidth = bounds.Width;
            mainHeight = bounds.Height - totalThumbnailHeight;
        }
        else
        {
            mainWidth = int.TryParse(MainWidthTextBox.Text, out int mw) ? mw : bounds.Width;
            mainHeight = int.TryParse(MainHeightTextBox.Text, out int mh) ? mh : bounds.Height - totalThumbnailHeight;
        }

        int thumbsPerRowActual = (int)Math.Ceiling((double)slotCount / rows);

        for (int i = 0; i < slotCount; i++)
        {
            int row = i / thumbsPerRowActual;
            int col = i % thumbsPerRowActual;

            int thumbX = bounds.X + col * (thumbnailWidth + gap);
            int thumbY = bounds.Y + mainHeight + row * (thumbnailHeight + gap);

            _slotRegions.Add(new SlotRegionViewModel
            {
                SlotId = i + 1,
                ForeX = bounds.X,
                ForeY = bounds.Y,
                ForeWidth = mainWidth,
                ForeHeight = mainHeight,
                BackX = thumbX,
                BackY = thumbY,
                BackWidth = thumbnailWidth,
                BackHeight = thumbnailHeight
            });
        }
    }

    private void GenerateVerticalLayout(MonitorRect bounds, int slotCount, int columns, double aspectRatio, int gap)
    {
        // Vertical: Main window on left, thumbnails on right in columns
        int thumbnailWidth, thumbnailHeight, thumbsPerColumn;

        if (ThumbAutoRadio.IsChecked == true)
        {
            // Use optimized vertical layout calculator
            var (calcWidth, calcHeight, calcCols, calcPerCol) =
                CalculateOptimalVerticalThumbnailLayout(bounds.Width, bounds.Height, slotCount);

            thumbnailWidth = calcWidth;
            thumbnailHeight = calcHeight;
            columns = calcCols;
            thumbsPerColumn = calcPerCol;
        }
        else
        {
            // Use custom sizes
            thumbnailWidth = int.TryParse(ThumbnailWidthTextBox.Text, out int tw) ? tw : 320;
            thumbnailHeight = int.TryParse(ThumbnailHeightTextBox.Text, out int th) ? th : 180;

            // Use the rows setting as columns for vertical layout
            columns = ThumbnailRowsComboBox.SelectedIndex + 1;
            thumbsPerColumn = (int)Math.Ceiling((double)slotCount / columns);
        }

        int totalThumbnailWidth = thumbnailWidth * columns + gap * (columns - 1);

        // Main region size
        int mainWidth, mainHeight;
        if (MainAutoRadio.IsChecked == true)
        {
            mainWidth = bounds.Width - totalThumbnailWidth;
            mainHeight = bounds.Height;
        }
        else
        {
            mainWidth = int.TryParse(MainWidthTextBox.Text, out int mw) ? mw : bounds.Width - totalThumbnailWidth;
            mainHeight = int.TryParse(MainHeightTextBox.Text, out int mh) ? mh : bounds.Height;
        }

        for (int i = 0; i < slotCount; i++)
        {
            int col = i / thumbsPerColumn;
            int row = i % thumbsPerColumn;

            int thumbX = bounds.X + mainWidth + col * (thumbnailWidth + gap);
            int thumbY = bounds.Y + row * (thumbnailHeight + gap);

            _slotRegions.Add(new SlotRegionViewModel
            {
                SlotId = i + 1,
                ForeX = bounds.X,
                ForeY = bounds.Y,
                ForeWidth = mainWidth,
                ForeHeight = mainHeight,
                BackX = thumbX,
                BackY = thumbY,
                BackWidth = thumbnailWidth,
                BackHeight = thumbnailHeight
            });
        }
    }

    private void GenerateGridLayout(MonitorRect bounds, int slotCount, int gap)
    {
        // Grid: All windows same size in a grid, foreground = full screen
        int cols = (int)Math.Ceiling(Math.Sqrt(slotCount));
        int rows = (int)Math.Ceiling((double)slotCount / cols);
        int cellWidth = (bounds.Width - (cols - 1) * gap) / cols;
        int cellHeight = (bounds.Height - (rows - 1) * gap) / rows;

        int slot = 0;
        for (int row = 0; row < rows && slot < slotCount; row++)
        {
            for (int col = 0; col < cols && slot < slotCount; col++)
            {
                _slotRegions.Add(new SlotRegionViewModel
                {
                    SlotId = slot + 1,
                    // Foreground = full screen (for when this slot is focused)
                    ForeX = bounds.X,
                    ForeY = bounds.Y,
                    ForeWidth = bounds.Width,
                    ForeHeight = bounds.Height,
                    // Background = grid cell
                    BackX = bounds.X + col * (cellWidth + gap),
                    BackY = bounds.Y + row * (cellHeight + gap),
                    BackWidth = cellWidth,
                    BackHeight = cellHeight
                });
                slot++;
            }
        }
    }

    private void GenerateStackedLayout(MonitorRect bounds, int slotCount)
    {
        // Stacked: All windows same position (stacked on top of each other)
        for (int i = 0; i < slotCount; i++)
        {
            _slotRegions.Add(new SlotRegionViewModel
            {
                SlotId = i + 1,
                ForeX = bounds.X,
                ForeY = bounds.Y,
                ForeWidth = bounds.Width,
                ForeHeight = bounds.Height,
                BackX = bounds.X,
                BackY = bounds.Y,
                BackWidth = bounds.Width,
                BackHeight = bounds.Height
            });
        }
    }

    private int GetSelectedSlotCount()
    {
        var item = SlotCountComboBox.SelectedItem as ComboBoxItem;
        if (item != null && int.TryParse(item.Content?.ToString(), out int count))
            return count;
        return 6;
    }

    private void CapturePositions_Click(object sender, RoutedEventArgs e)
    {
        var activeSlots = App.SlotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
        {
            MessageBox.Show("No active game windows to capture.", "No Windows", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _slotRegions.Clear();

        foreach (var slot in activeSlots)
        {
            if (slot.MainWindowHandle == IntPtr.Zero)
                continue;

            var (x, y, width, height) = WindowHelper.GetWindowPosition(slot.MainWindowHandle);

            _slotRegions.Add(new SlotRegionViewModel
            {
                SlotId = slot.Id,
                // Use current position for both fore and back (user can adjust)
                ForeX = x,
                ForeY = y,
                ForeWidth = width,
                ForeHeight = height,
                BackX = x,
                BackY = y,
                BackWidth = width,
                BackHeight = height
            });
        }

        MessageBox.Show($"Captured positions for {_slotRegions.Count} windows.", "Captured", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdatePreview();
    }

    private void PreviewLayout_Click(object sender, RoutedEventArgs e)
    {
        // Toggle through preview modes
        if (ShowBackgroundRadio.IsChecked == true)
        {
            ShowForegroundRadio.IsChecked = true;
        }
        else if (ShowForegroundRadio.IsChecked == true)
        {
            ShowBothRadio.IsChecked = true;
        }
        else
        {
            ShowBackgroundRadio.IsChecked = true;
        }
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        var activeSlots = App.SlotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
        {
            MessageBox.Show("No active game windows to apply layout to.", "No Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_slotRegions.Count == 0)
        {
            MessageBox.Show("No layout regions defined. Select or generate a layout first.", "No Layout", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int appliedCount = 0;

        // Apply layout by index (active slot 0 gets region 0, etc.) rather than by slot ID
        var sortedSlots = activeSlots.OrderBy(s => s.Id).ToList();
        var sortedRegions = _slotRegions.OrderBy(r => r.SlotId).ToList();

        // Like ISBoxer: ALL windows go to their BackRegion (thumbnail) position
        // The focused window will swap to ForeRegion when activated via hotkey
        for (int i = 0; i < Math.Min(sortedSlots.Count, sortedRegions.Count); i++)
        {
            var slot = sortedSlots[i];
            var regionVm = sortedRegions[i];

            if (slot.MainWindowHandle == IntPtr.Zero)
                continue;

            // Restore from maximized if needed
            if (WindowHelper.IsWindowMaximized(slot.MainWindowHandle))
            {
                WindowHelper.RestoreWindow(slot.MainWindowHandle);
                System.Threading.Thread.Sleep(50);
            }

            // Make borderless
            WindowHelper.MakeBorderless(slot.MainWindowHandle);

            appliedCount++;
        }

        // Wait for style changes
        System.Threading.Thread.Sleep(200);

        // Use DeferWindowPos to batch all window changes atomically (like JMB's -stealth flag)
        // This minimizes DirectX reset triggers
        int windowCount = Math.Min(sortedSlots.Count, sortedRegions.Count);
        IntPtr hdwp = Multiboxer.Native.User32.BeginDeferWindowPos(windowCount);

        if (hdwp != IntPtr.Zero)
        {
            for (int i = 0; i < windowCount; i++)
            {
                var slot = sortedSlots[i];
                var regionVm = sortedRegions[i];

                if (slot.MainWindowHandle == IntPtr.Zero)
                    continue;

                // All windows go to BackRegion
                hdwp = Multiboxer.Native.User32.DeferWindowPos(
                    hdwp,
                    slot.MainWindowHandle,
                    IntPtr.Zero,
                    regionVm.BackX,
                    regionVm.BackY,
                    regionVm.BackWidth,
                    regionVm.BackHeight,
                    Multiboxer.Native.SetWindowPosFlags.SWP_NOZORDER |
                    Multiboxer.Native.SetWindowPosFlags.SWP_NOACTIVATE);

                if (hdwp == IntPtr.Zero)
                    break;
            }

            if (hdwp != IntPtr.Zero)
            {
                Multiboxer.Native.User32.EndDeferWindowPos(hdwp);
            }
        }

        // Now bring the first window to ForeRegion
        if (sortedSlots.Count > 0 && sortedRegions.Count > 0)
        {
            var firstSlot = sortedSlots[0];
            var firstRegion = sortedRegions[0];

            if (firstSlot.MainWindowHandle != IntPtr.Zero)
            {
                // Move to foreground region
                Multiboxer.Native.User32.SetWindowPos(
                    firstSlot.MainWindowHandle,
                    IntPtr.Zero,
                    firstRegion.ForeX,
                    firstRegion.ForeY,
                    firstRegion.ForeWidth,
                    firstRegion.ForeHeight,
                    Multiboxer.Native.SetWindowPosFlags.SWP_NOZORDER |
                    Multiboxer.Native.SetWindowPosFlags.SWP_SHOWWINDOW);

                WindowHelper.ForceForegroundWindow(firstSlot.MainWindowHandle);
            }
        }

        // Store the regions in the layout engine for hotkey swaps
        var slotRegionsForEngine = new List<SlotRegion>();
        for (int i = 0; i < Math.Min(sortedSlots.Count, sortedRegions.Count); i++)
        {
            var slot = sortedSlots[i];
            var regionVm = sortedRegions[i];
            slotRegionsForEngine.Add(new SlotRegion
            {
                SlotId = slot.Id,
                ForeRegion = new WindowRect(regionVm.ForeX, regionVm.ForeY, regionVm.ForeWidth, regionVm.ForeHeight),
                BackRegion = new WindowRect(regionVm.BackX, regionVm.BackY, regionVm.BackWidth, regionVm.BackHeight)
            });
        }
        App.LayoutEngine.SetSlotRegions(slotRegionsForEngine);

        // Save active layout name so it auto-loads on next startup
        if (!string.IsNullOrWhiteSpace(LayoutNameTextBox.Text))
        {
            App.ConfigManager.Settings.ActiveSavedLayoutName = LayoutNameTextBox.Text;
            App.ConfigManager.Save();
        }

        // Debug: show BackRegion positions for first few slots
        string debugInfo = "\n\nBackRegion positions:";
        for (int i = 0; i < Math.Min(6, sortedRegions.Count); i++)
        {
            var r = sortedRegions[i];
            debugInfo += $"\n  Slot {r.SlotId}: {r.BackX},{r.BackY}";
        }

        MessageBox.Show($"Layout applied to {appliedCount} of {activeSlots.Count} windows.{debugInfo}",
            "Applied", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LayoutNameTextBox.Text))
        {
            MessageBox.Show("Please enter a layout name.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var layout = new SavedWindowLayout
        {
            Name = LayoutNameTextBox.Text,
            SlotRegions = _slotRegions.Select(vm => vm.ToSlotRegion()).ToList()
        };

        // Update or add
        int existingIndex = _savedLayouts.FindIndex(l => l.Name == layout.Name);
        if (existingIndex >= 0)
        {
            _savedLayouts[existingIndex] = layout;
        }
        else
        {
            _savedLayouts.Add(layout);
            _layoutNames.Add(layout.Name);
        }

        // Update the listbox name if changed
        if (LayoutsListBox.SelectedIndex >= 0)
        {
            _layoutNames[LayoutsListBox.SelectedIndex] = layout.Name;
        }

        // Save to config
        App.ConfigManager.Settings.SavedWindowLayouts = _savedLayouts;
        App.ConfigManager.Save();

        MessageBox.Show("Layout saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowThumbnails_Click(object sender, RoutedEventArgs e)
    {
        var activeSlots = App.SlotManager.GetActiveSlots().ToList();
        if (activeSlots.Count == 0)
        {
            MessageBox.Show("No active game windows to show thumbnails for.", "No Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_slotRegions.Count == 0)
        {
            MessageBox.Show("No layout regions defined. Select or generate a layout first.", "No Layout", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Build slot regions and apply to thumbnail manager
        var sortedSlots = activeSlots.OrderBy(s => s.Id).ToList();
        var sortedRegions = _slotRegions.OrderBy(r => r.SlotId).ToList();

        // Debug info
        var debugInfo = new System.Text.StringBuilder();

        // Show monitor info
        var monitor = MonitorManager.GetPrimaryMonitor();
        debugInfo.AppendLine($"Monitor: {monitor?.Width}x{monitor?.Height} (WorkArea: {monitor?.WorkingArea})");
        debugInfo.AppendLine($"Active slots: {sortedSlots.Count}");
        debugInfo.AppendLine($"Defined regions: {sortedRegions.Count}");

        foreach (var slot in sortedSlots)
        {
            debugInfo.AppendLine($"  Slot {slot.Id}: hwnd={slot.MainWindowHandle}, hasProcess={slot.HasProcess}");
        }

        // Store regions in layout engine first
        var slotRegionsForEngine = new List<SlotRegion>();
        for (int i = 0; i < Math.Min(sortedSlots.Count, sortedRegions.Count); i++)
        {
            var slot = sortedSlots[i];
            var regionVm = sortedRegions[i];
            slotRegionsForEngine.Add(new SlotRegion
            {
                SlotId = slot.Id,
                ForeRegion = new WindowRect(regionVm.ForeX, regionVm.ForeY, regionVm.ForeWidth, regionVm.ForeHeight),
                BackRegion = new WindowRect(regionVm.BackX, regionVm.BackY, regionVm.BackWidth, regionVm.BackHeight)
            });
            debugInfo.AppendLine($"  Region {slot.Id}: Back=({regionVm.BackX},{regionVm.BackY}) {regionVm.BackWidth}x{regionVm.BackHeight}");
        }
        App.LayoutEngine.SetSlotRegions(slotRegionsForEngine);

        // Apply thumbnails - first slot is foreground (no thumbnail), rest get thumbnails
        int foregroundSlotId = sortedSlots.Count > 0 ? sortedSlots[0].Id : 0;
        App.ThumbnailManager.ApplyLayout(sortedSlots, App.LayoutEngine.SlotRegions, foregroundSlotId);

        debugInfo.AppendLine($"\nForeground slot: {foregroundSlotId} (no thumbnail)");
        debugInfo.AppendLine($"Thumbnails created for: {sortedSlots.Count - 1} slots");

        MessageBox.Show(debugInfo.ToString(), "Thumbnail Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void HideThumbnails_Click(object sender, RoutedEventArgs e)
    {
        App.ThumbnailManager.HideAll();
        MessageBox.Show("Thumbnails hidden.", "Hidden", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PreviewMode_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (PreviewCanvas == null || PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0)
            return;

        PreviewCanvas.Children.Clear();

        if (_slotRegions.Count == 0)
        {
            PreviewInfoText.Text = "No slots defined";
            return;
        }

        // Calculate scale to fit monitor in canvas
        double canvasWidth = PreviewCanvas.ActualWidth;
        double canvasHeight = PreviewCanvas.ActualHeight;
        double scaleX = canvasWidth / _monitorBounds.Width;
        double scaleY = canvasHeight / _monitorBounds.Height;
        double scale = Math.Min(scaleX, scaleY) * 0.98; // 98% to maximize space

        // Calculate offset to center the preview
        double scaledWidth = _monitorBounds.Width * scale;
        double scaledHeight = _monitorBounds.Height * scale;
        double offsetX = (canvasWidth - scaledWidth) / 2;
        double offsetY = (canvasHeight - scaledHeight) / 2;

        // Draw monitor boundary
        var monitorRect = new WpfRectangle
        {
            Width = scaledWidth,
            Height = scaledHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        Canvas.SetLeft(monitorRect, offsetX);
        Canvas.SetTop(monitorRect, offsetY);
        PreviewCanvas.Children.Add(monitorRect);

        // Determine what to show
        bool showForeground = ShowForegroundRadio.IsChecked == true || ShowBothRadio.IsChecked == true;
        bool showBackground = ShowBackgroundRadio.IsChecked == true || ShowBothRadio.IsChecked == true;

        // Colors
        var foregroundBrush = new SolidColorBrush(Color.FromArgb(180, 76, 175, 80)); // Green with transparency
        var backgroundBrush = new SolidColorBrush(Color.FromArgb(200, 33, 150, 243)); // Blue with transparency
        var foregroundStroke = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        var backgroundStroke = new SolidColorBrush(Color.FromRgb(33, 150, 243));

        // Draw foreground regions first (so background overlays on top for "both" mode)
        if (showForeground && !showBackground)
        {
            foreach (var slot in _slotRegions)
            {
                DrawSlotRegion(slot, true, offsetX, offsetY, scale, foregroundBrush, foregroundStroke);
            }
        }

        // Draw background regions
        if (showBackground)
        {
            foreach (var slot in _slotRegions)
            {
                DrawSlotRegion(slot, false, offsetX, offsetY, scale, backgroundBrush, backgroundStroke);
            }
        }

        // For "both" mode, draw a semi-transparent foreground outline
        if (showForeground && showBackground && _slotRegions.Count > 0)
        {
            var firstSlot = _slotRegions[0];
            var foreOutline = new WpfRectangle
            {
                Width = Math.Max(1, firstSlot.ForeWidth * scale),
                Height = Math.Max(1, firstSlot.ForeHeight * scale),
                Stroke = foregroundStroke,
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(40, 76, 175, 80))
            };
            Canvas.SetLeft(foreOutline, offsetX + (firstSlot.ForeX - _monitorBounds.X) * scale);
            Canvas.SetTop(foreOutline, offsetY + (firstSlot.ForeY - _monitorBounds.Y) * scale);
            PreviewCanvas.Children.Add(foreOutline);

            // Add label for foreground
            var foreLabel = new TextBlock
            {
                Text = "Main Window",
                Foreground = foregroundStroke,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(foreLabel, offsetX + (firstSlot.ForeX - _monitorBounds.X) * scale + 8);
            Canvas.SetTop(foreLabel, offsetY + (firstSlot.ForeY - _monitorBounds.Y) * scale + 8);
            PreviewCanvas.Children.Add(foreLabel);
        }

        // Update info text
        PreviewInfoText.Text = $"Monitor: {_monitorBounds.Width}x{_monitorBounds.Height} | Slots: {_slotRegions.Count}";
    }

    private void DrawSlotRegion(SlotRegionViewModel slot, bool isForeground, double offsetX, double offsetY, double scale, SolidColorBrush fill, SolidColorBrush stroke)
    {
        int x = isForeground ? slot.ForeX : slot.BackX;
        int y = isForeground ? slot.ForeY : slot.BackY;
        int width = isForeground ? slot.ForeWidth : slot.BackWidth;
        int height = isForeground ? slot.ForeHeight : slot.BackHeight;

        double scaledX = offsetX + (x - _monitorBounds.X) * scale;
        double scaledY = offsetY + (y - _monitorBounds.Y) * scale;
        double scaledWidth = Math.Max(1, width * scale);
        double scaledHeight = Math.Max(1, height * scale);

        var rect = new WpfRectangle
        {
            Width = scaledWidth,
            Height = scaledHeight,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = 1
        };
        Canvas.SetLeft(rect, scaledX);
        Canvas.SetTop(rect, scaledY);
        PreviewCanvas.Children.Add(rect);

        // Add slot number label
        var label = new TextBlock
        {
            Text = slot.SlotId.ToString(),
            Foreground = Brushes.White,
            FontSize = Math.Min(12, Math.Max(8, scaledHeight * 0.6)),
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };

        // Center the label in the rectangle
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double labelX = scaledX + (scaledWidth - label.DesiredSize.Width) / 2;
        double labelY = scaledY + (scaledHeight - label.DesiredSize.Height) / 2;

        Canvas.SetLeft(label, labelX);
        Canvas.SetTop(label, labelY);
        PreviewCanvas.Children.Add(label);
    }
}
