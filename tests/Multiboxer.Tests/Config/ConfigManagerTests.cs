using FluentAssertions;
using Multiboxer.Core.Config;
using System.Text.Json;
using Xunit;

namespace Multiboxer.Tests.Config;

/// <summary>
/// Tests for ConfigManager - configuration file I/O
/// </summary>
public class ConfigManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ConfigManager _configManager;

    public ConfigManagerTests()
    {
        // Use a unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), "MultiboxerTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _configManager = new ConfigManager(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    #region Directory Tests

    [Fact]
    public void EnsureDirectoriesExist_ShouldCreateConfigDirectory()
    {
        // Act
        _configManager.EnsureDirectoriesExist();

        // Assert
        Directory.Exists(_configManager.ConfigDirectory).Should().BeTrue();
    }

    [Fact]
    public void EnsureDirectoriesExist_ShouldCreateProfilesDirectory()
    {
        // Act
        _configManager.EnsureDirectoriesExist();

        // Assert
        Directory.Exists(_configManager.ProfilesDirectory).Should().BeTrue();
    }

    [Fact]
    public void EnsureDirectoriesExist_ShouldCreateLayoutsDirectory()
    {
        // Act
        _configManager.EnsureDirectoriesExist();

        // Assert
        Directory.Exists(_configManager.LayoutsDirectory).Should().BeTrue();
    }

    #endregion

    #region Load/Save Tests

    [Fact]
    public void Save_ThenLoad_ShouldPreserveSettings()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        _configManager.Settings.Highlighter.BorderThickness = 5.0;
        _configManager.Settings.Performance.BackgroundMaxFps = 15;

        // Act
        _configManager.Save();

        var newManager = new ConfigManager(_testDirectory);
        newManager.Load();

        // Assert
        newManager.Settings.Highlighter.BorderThickness.Should().Be(5.0);
        newManager.Settings.Performance.BackgroundMaxFps.Should().Be(15);
    }

    [Fact]
    public void Load_WithNoExistingFile_ShouldUseDefaults()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();

        // Act
        _configManager.Load();

        // Assert
        _configManager.Settings.Should().NotBeNull();
        _configManager.Settings.Hotkeys.GlobalHotkeysEnabled.Should().BeTrue();
    }

    [Fact]
    public void ResetToDefaults_ShouldRestoreDefaultSettings()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        _configManager.Settings.Highlighter.BorderThickness = 99.0;
        _configManager.Save();

        // Act
        _configManager.ResetToDefaults();

        // Assert
        _configManager.Settings.Highlighter.BorderThickness.Should().Be(
            HighlighterSettings.CreateDefault().BorderThickness);
    }

    #endregion

    #region Profile Tests

    [Fact]
    public void SaveProfile_ShouldCreateProfileFile()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var profile = new LaunchProfile
        {
            Name = "TestProfile",
            Path = @"C:\Test",
            Executable = "test.exe"
        };

        // Act
        _configManager.SaveProfile(profile);

        // Assert
        var profilePath = Path.Combine(_configManager.ProfilesDirectory, "TestProfile.json");
        File.Exists(profilePath).Should().BeTrue();
    }

    [Fact]
    public void SaveProfile_ShouldAddToSettingsProfiles()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var profile = new LaunchProfile
        {
            Name = "TestProfile",
            Path = @"C:\Test",
            Executable = "test.exe"
        };

        // Act
        _configManager.SaveProfile(profile);

        // Assert
        _configManager.Settings.Profiles.Should().Contain(p => p.Name == "TestProfile");
    }

    [Fact]
    public void DeleteProfile_ShouldRemoveProfileFile()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var profile = new LaunchProfile
        {
            Name = "ToDelete",
            Path = @"C:\Test",
            Executable = "test.exe"
        };
        _configManager.SaveProfile(profile);
        var profilePath = Path.Combine(_configManager.ProfilesDirectory, "ToDelete.json");
        File.Exists(profilePath).Should().BeTrue();

        // Act
        _configManager.DeleteProfile("ToDelete");

        // Assert
        File.Exists(profilePath).Should().BeFalse();
    }

    [Fact]
    public void DeleteProfile_ShouldRemoveFromSettingsProfiles()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var profile = new LaunchProfile
        {
            Name = "ToDelete",
            Path = @"C:\Test",
            Executable = "test.exe"
        };
        _configManager.SaveProfile(profile);

        // Act
        _configManager.DeleteProfile("ToDelete");

        // Assert
        _configManager.Settings.Profiles.Should().NotContain(p => p.Name == "ToDelete");
    }

    #endregion

    #region Export/Import Tests

    [Fact]
    public void Export_ShouldCreateValidJsonFile()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        _configManager.Settings.Performance.BackgroundMaxFps = 25;
        var exportPath = Path.Combine(_testDirectory, "export.json");

        // Act
        _configManager.Export(exportPath);

        // Assert
        File.Exists(exportPath).Should().BeTrue();
        var json = File.ReadAllText(exportPath);
        json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Import_ShouldLoadSettingsFromFile()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var settings = AppSettings.CreateDefault();
        settings.Highlighter.BorderThickness = 7.5;

        var importPath = Path.Combine(_testDirectory, "import.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(importPath, JsonSerializer.Serialize(settings, jsonOptions));

        // Act
        var result = _configManager.Import(importPath);

        // Assert
        result.Should().BeTrue();
        _configManager.Settings.Highlighter.BorderThickness.Should().Be(7.5);
    }

    [Fact]
    public void Import_WithInvalidFile_ShouldReturnFalse()
    {
        // Arrange
        _configManager.EnsureDirectoriesExist();
        var importPath = Path.Combine(_testDirectory, "invalid.json");
        File.WriteAllText(importPath, "not valid json {{{");

        // Act
        var result = _configManager.Import(importPath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Import_WithNonexistentFile_ShouldReturnFalse()
    {
        // Arrange
        var importPath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act
        var result = _configManager.Import(importPath);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Settings Property Tests

    [Fact]
    public void Settings_ShouldBeInitializedToDefaults()
    {
        // Assert
        _configManager.Settings.Should().NotBeNull();
        _configManager.Settings.Version.Should().Be("1.0");
    }

    [Fact]
    public void ConfigDirectory_ShouldMatchConstructorArgument()
    {
        // Assert
        _configManager.ConfigDirectory.Should().Be(_testDirectory);
    }

    [Fact]
    public void ProfilesDirectory_ShouldBeSubdirectoryOfConfigDirectory()
    {
        // Assert
        _configManager.ProfilesDirectory.Should().StartWith(_testDirectory);
        _configManager.ProfilesDirectory.Should().Contain("profiles");
    }

    [Fact]
    public void LayoutsDirectory_ShouldBeSubdirectoryOfConfigDirectory()
    {
        // Assert
        _configManager.LayoutsDirectory.Should().StartWith(_testDirectory);
        _configManager.LayoutsDirectory.Should().Contain("layouts");
    }

    #endregion
}
