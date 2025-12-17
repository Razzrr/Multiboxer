using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Multiboxer.Core.Config;

namespace Multiboxer.App.Views;

/// <summary>
/// Dialog for managing launch profiles
/// </summary>
public partial class ProfileManagerDialog : Window
{
    private readonly ObservableCollection<LaunchProfile> _profiles;

    public ProfileManagerDialog()
    {
        InitializeComponent();

        _profiles = new ObservableCollection<LaunchProfile>(App.ConfigManager.Settings.Profiles);
        ProfilesListView.ItemsSource = _profiles;
    }

    private void ProfilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = ProfilesListView.SelectedItem != null;
        EditButton.IsEnabled = hasSelection;
        DuplicateButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        ExportButton.IsEnabled = hasSelection;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileEditorDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _profiles.Add(dialog.Result);
            App.ConfigManager.SaveProfile(dialog.Result);
            ProfilesListView.SelectedItem = dialog.Result;
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var selectedProfile = ProfilesListView.SelectedItem as LaunchProfile;
        if (selectedProfile == null)
            return;

        var dialog = new ProfileEditorDialog(selectedProfile)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            // Update in collection
            var index = _profiles.IndexOf(selectedProfile);
            if (index >= 0)
            {
                _profiles[index] = dialog.Result;
            }

            App.ConfigManager.SaveProfile(dialog.Result);
            ProfilesListView.SelectedItem = dialog.Result;
        }
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        var selectedProfile = ProfilesListView.SelectedItem as LaunchProfile;
        if (selectedProfile == null)
            return;

        // Create a copy with a new name
        var copy = new LaunchProfile
        {
            Name = $"{selectedProfile.Name} (Copy)",
            Game = selectedProfile.Game,
            Path = selectedProfile.Path,
            Executable = selectedProfile.Executable,
            Arguments = selectedProfile.Arguments,
            RunAsAdmin = selectedProfile.RunAsAdmin,
            LaunchDelay = selectedProfile.LaunchDelay,
            WindowClass = selectedProfile.WindowClass,
            WindowTitlePattern = selectedProfile.WindowTitlePattern,
            UseVirtualFiles = selectedProfile.UseVirtualFiles,
            VirtualFiles = selectedProfile.VirtualFiles.ToList()
        };

        // Open editor for the copy
        var dialog = new ProfileEditorDialog(copy)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _profiles.Add(dialog.Result);
            App.ConfigManager.SaveProfile(dialog.Result);
            ProfilesListView.SelectedItem = dialog.Result;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selectedProfile = ProfilesListView.SelectedItem as LaunchProfile;
        if (selectedProfile == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the profile '{selectedProfile.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _profiles.Remove(selectedProfile);
            App.ConfigManager.DeleteProfile(selectedProfile.Name);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(dialog.FileName);
                var profile = JsonSerializer.Deserialize<LaunchProfile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (profile != null)
                {
                    // Check for duplicate name
                    if (_profiles.Any(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        profile.Name = $"{profile.Name} (Imported)";
                    }

                    _profiles.Add(profile);
                    App.ConfigManager.SaveProfile(profile);
                    ProfilesListView.SelectedItem = profile;

                    MessageBox.Show($"Profile '{profile.Name}' imported successfully.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import profile: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var selectedProfile = ProfilesListView.SelectedItem as LaunchProfile;
        if (selectedProfile == null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Profile",
            Filter = "JSON files (*.json)|*.json",
            FileName = $"{selectedProfile.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(selectedProfile, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(dialog.FileName, json);

                MessageBox.Show($"Profile exported to {dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export profile: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Update the main settings with any changes
        App.ConfigManager.Settings.Profiles = _profiles.ToList();
        DialogResult = true;
        Close();
    }
}
