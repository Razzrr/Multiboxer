using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using Multiboxer.Core.Config;

namespace Multiboxer.App.Views;

/// <summary>
/// Dialog for editing launch profiles
/// </summary>
public partial class ProfileEditorDialog : Window
{
    private readonly LaunchProfile _profile;
    private readonly bool _isNew;
    private readonly ObservableCollection<VirtualFileMapping> _virtualFiles = new();

    /// <summary>
    /// The edited profile (null if cancelled)
    /// </summary>
    public LaunchProfile? Result { get; private set; }

    public ProfileEditorDialog(LaunchProfile? existingProfile = null)
    {
        InitializeComponent();

        _isNew = existingProfile == null;
        _profile = existingProfile ?? new LaunchProfile();

        Title = _isNew ? "New Profile" : "Edit Profile";

        VirtualFilesListBox.ItemsSource = _virtualFiles;

        LoadProfile();
    }

    private void LoadProfile()
    {
        ProfileNameTextBox.Text = _profile.Name;
        GameNameTextBox.Text = _profile.Game;
        PathTextBox.Text = _profile.Path;
        ExecutableTextBox.Text = _profile.Executable;
        GameExecutableTextBox.Text = _profile.GameExecutable ?? string.Empty;
        ArgumentsTextBox.Text = _profile.Arguments;
        RunAsAdminCheckBox.IsChecked = _profile.RunAsAdmin;
        LaunchDelayTextBox.Text = _profile.LaunchDelay.ToString();
        WindowClassTextBox.Text = _profile.WindowClass ?? string.Empty;
        WindowTitlePatternTextBox.Text = _profile.WindowTitlePattern ?? string.Empty;

        // Load virtual files
        UseVirtualFilesCheckBox.IsChecked = _profile.UseVirtualFiles;
        _virtualFiles.Clear();
        foreach (var vf in _profile.VirtualFiles)
        {
            _virtualFiles.Add(vf);
        }
    }

    private bool ValidateAndSave()
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
        {
            MessageBox.Show("Profile name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ProfileNameTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(PathTextBox.Text))
        {
            MessageBox.Show("Game path is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            PathTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(ExecutableTextBox.Text))
        {
            MessageBox.Show("Executable is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            ExecutableTextBox.Focus();
            return false;
        }

        // Validate path exists
        if (!System.IO.Directory.Exists(PathTextBox.Text))
        {
            var result = MessageBox.Show(
                "The specified path does not exist. Save anyway?",
                "Path Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                PathTextBox.Focus();
                return false;
            }
        }

        // Validate executable exists
        var fullPath = System.IO.Path.Combine(PathTextBox.Text, ExecutableTextBox.Text);
        if (!System.IO.File.Exists(fullPath))
        {
            var result = MessageBox.Show(
                "The specified executable does not exist. Save anyway?",
                "Executable Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                ExecutableTextBox.Focus();
                return false;
            }
        }

        // Parse launch delay
        if (!int.TryParse(LaunchDelayTextBox.Text, out var launchDelay) || launchDelay < 0)
        {
            launchDelay = 0;
        }

        // Update profile
        _profile.Name = ProfileNameTextBox.Text.Trim();
        _profile.Game = GameNameTextBox.Text.Trim();
        _profile.Path = PathTextBox.Text.Trim();
        _profile.Executable = ExecutableTextBox.Text.Trim();
        _profile.GameExecutable = string.IsNullOrWhiteSpace(GameExecutableTextBox.Text) ? null : GameExecutableTextBox.Text.Trim();
        _profile.Arguments = ArgumentsTextBox.Text.Trim();
        _profile.RunAsAdmin = RunAsAdminCheckBox.IsChecked ?? false;
        _profile.LaunchDelay = launchDelay;
        _profile.WindowClass = string.IsNullOrWhiteSpace(WindowClassTextBox.Text) ? null : WindowClassTextBox.Text.Trim();
        _profile.WindowTitlePattern = string.IsNullOrWhiteSpace(WindowTitlePatternTextBox.Text) ? null : WindowTitlePatternTextBox.Text.Trim();

        // Update virtual files
        _profile.UseVirtualFiles = UseVirtualFilesCheckBox.IsChecked ?? false;
        _profile.VirtualFiles.Clear();
        foreach (var vf in _virtualFiles)
        {
            _profile.VirtualFiles.Add(vf);
        }

        Result = _profile;
        return true;
    }

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Game Folder"
        };

        if (!string.IsNullOrEmpty(PathTextBox.Text) && System.IO.Directory.Exists(PathTextBox.Text))
        {
            dialog.InitialDirectory = PathTextBox.Text;
        }

        if (dialog.ShowDialog() == true)
        {
            PathTextBox.Text = dialog.FolderName;
        }
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Launcher/Game Executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrEmpty(PathTextBox.Text) && System.IO.Directory.Exists(PathTextBox.Text))
        {
            dialog.InitialDirectory = PathTextBox.Text;
        }

        if (dialog.ShowDialog() == true)
        {
            // If the file is in the path directory, just use the filename
            var directory = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (directory != null && directory.Equals(PathTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                ExecutableTextBox.Text = System.IO.Path.GetFileName(dialog.FileName);
            }
            else
            {
                // Update path to the directory and use filename
                PathTextBox.Text = directory ?? string.Empty;
                ExecutableTextBox.Text = System.IO.Path.GetFileName(dialog.FileName);
            }
        }
    }

    private void BrowseGameExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Game Executable (if different from launcher)",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrEmpty(PathTextBox.Text) && System.IO.Directory.Exists(PathTextBox.Text))
        {
            dialog.InitialDirectory = PathTextBox.Text;
        }

        if (dialog.ShowDialog() == true)
        {
            // Just use the filename
            GameExecutableTextBox.Text = System.IO.Path.GetFileName(dialog.FileName);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateAndSave())
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AddVirtualFile_Click(object sender, RoutedEventArgs e)
    {
        var pattern = VirtualFilePatternTextBox.Text.Trim();
        if (string.IsNullOrEmpty(pattern))
        {
            MessageBox.Show("Please enter a file pattern.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check for duplicates
        if (_virtualFiles.Any(vf => vf.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("This file pattern is already added.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create default replacement pattern
        var ext = System.IO.Path.GetExtension(pattern);
        var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(pattern);
        var replacement = $"{nameWithoutExt}.{{slot}}{ext}";

        _virtualFiles.Add(new VirtualFileMapping
        {
            Pattern = pattern,
            Replacement = replacement
        });

        VirtualFilePatternTextBox.Clear();
    }

    private void RemoveVirtualFile_Click(object sender, RoutedEventArgs e)
    {
        if (VirtualFilesListBox.SelectedItem is VirtualFileMapping mapping)
        {
            _virtualFiles.Remove(mapping);
        }
    }
}
