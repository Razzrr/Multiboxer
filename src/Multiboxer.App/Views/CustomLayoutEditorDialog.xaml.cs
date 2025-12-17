using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Multiboxer.Core.Layout;

namespace Multiboxer.App.Views;

/// <summary>
/// Dialog for creating and editing custom layouts
/// </summary>
public partial class CustomLayoutEditorDialog : Window
{
    private ObservableCollection<CustomLayout> _layouts = new();
    private ObservableCollection<RegionViewModel> _regions = new();
    private CustomLayout? _currentLayout;

    /// <summary>
    /// The resulting layout if one was created/saved
    /// </summary>
    public CustomLayout? ResultLayout { get; private set; }

    public CustomLayoutEditorDialog()
    {
        InitializeComponent();

        // Load existing custom layouts
        foreach (var layout in App.LayoutEngine.CustomLayouts)
        {
            _layouts.Add(layout);
        }

        LayoutListBox.ItemsSource = _layouts;
        RegionsDataGrid.ItemsSource = _regions;

        if (_layouts.Count > 0)
        {
            LayoutListBox.SelectedIndex = 0;
        }
    }

    private void LoadLayout(CustomLayout layout)
    {
        _currentLayout = layout;
        LayoutNameTextBox.Text = layout.Name;

        _regions.Clear();

        // Add main region first
        _regions.Add(new RegionViewModel
        {
            SlotIndex = "Main",
            X = layout.MainRegion.X,
            Y = layout.MainRegion.Y,
            Width = layout.MainRegion.Width,
            Height = layout.MainRegion.Height,
            Description = "Main (foreground) window",
            IsMain = true
        });

        // Add other regions
        for (int i = 0; i < layout.Regions.Count; i++)
        {
            var region = layout.Regions[i];
            _regions.Add(new RegionViewModel
            {
                SlotIndex = (i + 2).ToString(),
                X = region.X,
                Y = region.Y,
                Width = region.Width,
                Height = region.Height,
                Description = $"Background slot {i + 2}",
                IsMain = false
            });
        }
    }

    private void SaveCurrentLayout()
    {
        if (_currentLayout == null)
            return;

        _currentLayout.Name = LayoutNameTextBox.Text.Trim();

        // Update main region
        var mainRegion = _regions.FirstOrDefault(r => r.IsMain);
        if (mainRegion != null)
        {
            _currentLayout.MainRegion = new WindowRegion
            {
                X = mainRegion.X,
                Y = mainRegion.Y,
                Width = mainRegion.Width,
                Height = mainRegion.Height,
                UsePercentage = true
            };
        }

        // Update other regions
        _currentLayout.Regions.Clear();
        foreach (var region in _regions.Where(r => !r.IsMain))
        {
            _currentLayout.Regions.Add(new WindowRegion
            {
                X = region.X,
                Y = region.Y,
                Width = region.Width,
                Height = region.Height,
                UsePercentage = true
            });
        }

        // Update in layout engine
        App.LayoutEngine.AddCustomLayout(_currentLayout);

        // Save to config
        UpdateConfigCustomLayouts();
    }

    private void UpdateConfigCustomLayouts()
    {
        if (App.ConfigManager.Settings.Layout == null)
        {
            App.ConfigManager.Settings.Layout = new Core.Config.LayoutSettings();
        }
        App.ConfigManager.Settings.Layout.CustomLayouts = App.LayoutEngine.CustomLayouts.ToList();
        App.ConfigManager.Save();
    }

    private void LayoutListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutListBox.SelectedItem is CustomLayout layout)
        {
            LoadLayout(layout);
        }
    }

    private void NewLayout_Click(object sender, RoutedEventArgs e)
    {
        var layout = new CustomLayout
        {
            Name = $"Custom Layout {_layouts.Count + 1}",
            MainRegion = new WindowRegion
            {
                X = 0,
                Y = 0,
                Width = 100,
                Height = 100,
                UsePercentage = true
            }
        };

        _layouts.Add(layout);
        LayoutListBox.SelectedItem = layout;
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if (LayoutListBox.SelectedItem is CustomLayout layout)
        {
            var result = MessageBox.Show(
                $"Delete layout '{layout.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _layouts.Remove(layout);
                App.LayoutEngine.RemoveCustomLayout(layout.Name);
                UpdateConfigCustomLayouts();

                _currentLayout = null;
                _regions.Clear();
                LayoutNameTextBox.Text = string.Empty;
            }
        }
    }

    private void AddRegion_Click(object sender, RoutedEventArgs e)
    {
        var slotNumber = _regions.Count + 1;
        _regions.Add(new RegionViewModel
        {
            SlotIndex = slotNumber.ToString(),
            X = 0,
            Y = 0,
            Width = 25,
            Height = 25,
            Description = $"Background slot {slotNumber}",
            IsMain = false
        });
    }

    private void RemoveRegion_Click(object sender, RoutedEventArgs e)
    {
        if (RegionsDataGrid.SelectedItem is RegionViewModel region && !region.IsMain)
        {
            _regions.Remove(region);

            // Renumber slots
            int index = 1;
            foreach (var r in _regions)
            {
                if (r.IsMain)
                {
                    r.SlotIndex = "Main";
                }
                else
                {
                    index++;
                    r.SlotIndex = index.ToString();
                    r.Description = $"Background slot {index}";
                }
            }
        }
    }

    private void CreateGrid2x2_Click(object sender, RoutedEventArgs e)
    {
        CreateGridLayout("Grid 2x2", 2, 2);
    }

    private void CreateGrid3x3_Click(object sender, RoutedEventArgs e)
    {
        CreateGridLayout("Grid 3x3", 3, 3);
    }

    private void CreateGridLayout(string name, int columns, int rows)
    {
        var layout = CustomLayout.CreateGridLayout(name, columns, rows);
        _layouts.Add(layout);
        App.LayoutEngine.AddCustomLayout(layout);
        UpdateConfigCustomLayouts();
        LayoutListBox.SelectedItem = layout;
    }

    private void CreatePiP3_Click(object sender, RoutedEventArgs e)
    {
        CreatePiPLayout("PiP (3 windows)", 2);
    }

    private void CreatePiP5_Click(object sender, RoutedEventArgs e)
    {
        CreatePiPLayout("PiP (5 windows)", 4);
    }

    private void CreatePiPLayout(string name, int pipCount)
    {
        var layout = CustomLayout.CreatePiPLayout(name, pipCount);
        _layouts.Add(layout);
        App.LayoutEngine.AddCustomLayout(layout);
        UpdateConfigCustomLayouts();
        LayoutListBox.SelectedItem = layout;
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LayoutNameTextBox.Text))
        {
            MessageBox.Show("Please enter a layout name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveCurrentLayout();
        ResultLayout = _currentLayout;
        MessageBox.Show($"Layout '{_currentLayout?.Name}' saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = ResultLayout != null;
        Close();
    }
}

/// <summary>
/// View model for editing a region
/// </summary>
public class RegionViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _slotIndex = "";
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private string _description = "";
    private bool _isMain;

    public string SlotIndex
    {
        get => _slotIndex;
        set { _slotIndex = value; OnPropertyChanged(); }
    }

    public int X
    {
        get => _x;
        set { _x = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
    }

    public int Y
    {
        get => _y;
        set { _y = Math.Clamp(value, 0, 100); OnPropertyChanged(); }
    }

    public int Width
    {
        get => _width;
        set { _width = Math.Clamp(value, 1, 100); OnPropertyChanged(); }
    }

    public int Height
    {
        get => _height;
        set { _height = Math.Clamp(value, 1, 100); OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public bool IsMain
    {
        get => _isMain;
        set { _isMain = value; OnPropertyChanged(); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
