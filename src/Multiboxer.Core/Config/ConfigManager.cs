using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiboxer.Core.Config;

/// <summary>
/// Manages application configuration persistence
/// </summary>
public class ConfigManager
{
    private readonly string _configDirectory;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Current application settings
    /// </summary>
    public AppSettings Settings { get; private set; }

    /// <summary>
    /// Path to the configuration directory
    /// </summary>
    public string ConfigDirectory => _configDirectory;

    /// <summary>
    /// Path to the profiles directory
    /// </summary>
    public string ProfilesDirectory => Path.Combine(_configDirectory, "profiles");

    /// <summary>
    /// Path to the layouts directory
    /// </summary>
    public string LayoutsDirectory => Path.Combine(_configDirectory, "layouts");

    public ConfigManager(string configDirectory)
    {
        _configDirectory = configDirectory;
        _settingsFilePath = Path.Combine(_configDirectory, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        Settings = AppSettings.CreateDefault();
    }

    /// <summary>
    /// Ensure configuration directories exist
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_configDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(LayoutsDirectory);
    }

    /// <summary>
    /// Load settings from disk
    /// </summary>
    public void Load()
    {
        EnsureDirectoriesExist();

        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                if (settings != null)
                {
                    Settings = settings;
                }
            }
            catch (Exception ex)
            {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
                // Keep default settings on error
            }
        }

        // Load profiles from separate files
        LoadProfiles();

        // Load custom layouts from separate files
        LoadCustomLayouts();
    }

    /// <summary>
    /// Save settings to disk
    /// </summary>
    public void Save()
    {
        EnsureDirectoriesExist();

        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);

            // Save profiles to separate files
            SaveProfiles();

            // Save custom layouts to separate files
            SaveCustomLayouts();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Load launch profiles from profiles directory
    /// </summary>
    private void LoadProfiles()
    {
        var profileFiles = Directory.GetFiles(ProfilesDirectory, "*.json");

        foreach (var file in profileFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<LaunchProfile>(json, _jsonOptions);

                if (profile != null && !Settings.Profiles.Any(p => p.Name == profile.Name))
                {
                    Settings.Profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load profile {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Save launch profiles to separate files
    /// </summary>
    private void SaveProfiles()
    {
        foreach (var profile in Settings.Profiles)
        {
            try
            {
                var fileName = SanitizeFileName(profile.Name) + ".json";
                var filePath = Path.Combine(ProfilesDirectory, fileName);
                var json = JsonSerializer.Serialize(profile, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save profile {profile.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Load custom layouts from layouts directory
    /// </summary>
    private void LoadCustomLayouts()
    {
        if (Settings.Layout == null)
            return;

        var layoutFiles = Directory.GetFiles(LayoutsDirectory, "*.json");

        foreach (var file in layoutFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var layout = JsonSerializer.Deserialize<Layout.CustomLayout>(json, _jsonOptions);

                if (layout != null && !Settings.Layout.CustomLayouts.Any(l => l.Name == layout.Name))
                {
                    Settings.Layout.CustomLayouts.Add(layout);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load layout {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Save custom layouts to separate files
    /// </summary>
    private void SaveCustomLayouts()
    {
        if (Settings.Layout?.CustomLayouts == null)
            return;

        foreach (var layout in Settings.Layout.CustomLayouts)
        {
            try
            {
                var fileName = SanitizeFileName(layout.Name) + ".json";
                var filePath = Path.Combine(LayoutsDirectory, fileName);
                var json = JsonSerializer.Serialize(layout, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save layout {layout.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Add or update a launch profile
    /// </summary>
    public void SaveProfile(LaunchProfile profile)
    {
        var existing = Settings.Profiles.FirstOrDefault(p => p.Name == profile.Name);
        if (existing != null)
        {
            Settings.Profiles.Remove(existing);
        }
        Settings.Profiles.Add(profile);

        // Save immediately
        try
        {
            var fileName = SanitizeFileName(profile.Name) + ".json";
            var filePath = Path.Combine(ProfilesDirectory, fileName);
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save profile {profile.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a launch profile
    /// </summary>
    public void DeleteProfile(string profileName)
    {
        var profile = Settings.Profiles.FirstOrDefault(p => p.Name == profileName);
        if (profile != null)
        {
            Settings.Profiles.Remove(profile);

            // Delete file
            try
            {
                var fileName = SanitizeFileName(profileName) + ".json";
                var filePath = Path.Combine(ProfilesDirectory, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete profile {profileName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sanitize a string for use as a filename
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    /// <summary>
    /// Reset settings to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        Settings = AppSettings.CreateDefault();
        Save();
    }

    /// <summary>
    /// Export settings to a file
    /// </summary>
    public void Export(string filePath)
    {
        var json = JsonSerializer.Serialize(Settings, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Import settings from a file
    /// </summary>
    public bool Import(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

            if (settings != null)
            {
                Settings = settings;
                Save();
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to import settings: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Import settings from Joe Multiboxer Basic Core configuration
    /// </summary>
    public bool ImportFromJoeMultiboxer(string settingsFilePath)
    {
        try
        {
            var json = File.ReadAllText(settingsFilePath);
            var jmbSettings = JsonSerializer.Deserialize<JoeMultiboxerSettings>(json, _jsonOptions);

            if (jmbSettings == null)
                return false;

            // Import launch profiles
            if (jmbSettings.Launcher?.Profiles != null)
            {
                foreach (var jmbProfile in jmbSettings.Launcher.Profiles)
                {
                    var profile = new LaunchProfile
                    {
                        Name = jmbProfile.Name ?? "Unnamed",
                        Game = jmbProfile.Game ?? "",
                        Path = jmbProfile.Path ?? "",
                        Executable = jmbProfile.Executable ?? "",
                        Arguments = jmbProfile.Parameters ?? "",
                        UseVirtualFiles = jmbProfile.UseDefaultVirtualFiles
                    };

                    // Import virtual files if present
                    if (jmbProfile.VirtualFiles != null)
                    {
                        foreach (var vf in jmbProfile.VirtualFiles)
                        {
                            profile.VirtualFiles.Add(new VirtualFileMapping
                            {
                                Pattern = vf.Pattern ?? "",
                                Replacement = vf.Replacement ?? ""
                            });
                        }
                    }

                    // Add or update profile
                    var existing = Settings.Profiles.FirstOrDefault(p => p.Name == profile.Name);
                    if (existing != null)
                    {
                        Settings.Profiles.Remove(existing);
                    }
                    Settings.Profiles.Add(profile);
                }
            }

            // Import highlighter settings
            if (jmbSettings.Highlighter != null)
            {
                Settings.Highlighter ??= HighlighterSettings.CreateDefault();
                Settings.Highlighter.ShowBorder = jmbSettings.Highlighter.ShowBorder;
                Settings.Highlighter.ShowNumber = jmbSettings.Highlighter.ShowNumber;
            }

            // Import hotkey settings
            if (jmbSettings.Hotkeys != null)
            {
                Settings.Hotkeys ??= HotkeySettings.CreateDefault();

                if (jmbSettings.Hotkeys.SlotHotkeys != null)
                {
                    Settings.Hotkeys.SlotHotkeys = jmbSettings.Hotkeys.SlotHotkeys
                        .Where(h => h != "NONE")
                        .ToList();
                }

                if (!string.IsNullOrEmpty(jmbSettings.Hotkeys.PreviousWindow))
                    Settings.Hotkeys.PreviousWindow = jmbSettings.Hotkeys.PreviousWindow;

                if (!string.IsNullOrEmpty(jmbSettings.Hotkeys.NextWindow))
                    Settings.Hotkeys.NextWindow = jmbSettings.Hotkeys.NextWindow;

                Settings.Hotkeys.GlobalHotkeysEnabled = jmbSettings.Hotkeys.GlobalSwitchingHotkeys;
            }

            // Import performance settings
            if (jmbSettings.Performance != null)
            {
                Settings.Performance ??= PerformanceSettings.CreateDefault();
                Settings.Performance.LockAffinity = jmbSettings.Performance.LockAffinity;

                if (jmbSettings.Performance.Background != null)
                    Settings.Performance.BackgroundMaxFps = jmbSettings.Performance.Background.MaxFPS;

                if (jmbSettings.Performance.Foreground != null)
                    Settings.Performance.ForegroundMaxFps = jmbSettings.Performance.Foreground.MaxFPS;
            }

            // Import window layout settings
            if (jmbSettings.WindowLayout != null)
            {
                Settings.Layout ??= LayoutSettings.CreateDefault();

                // Map JMB layout style to our layout name
                var activeLayoutName = jmbSettings.WindowLayout.UseLayout ?? "Horizontal";

                // Find the layout definition to get its style
                var layoutDef = jmbSettings.WindowLayout.Layouts?
                    .FirstOrDefault(l => l.Name == activeLayoutName);

                if (layoutDef != null)
                {
                    Settings.Layout.ActiveLayout = layoutDef.Style?.ToLower() switch
                    {
                        "horizontal" => "Horizontal",
                        "vertical" => "Vertical",
                        "custom" => activeLayoutName,
                        _ => "Horizontal"
                    };

                    Settings.Layout.Options.SwapOnActivate = layoutDef.SwapOnActivate;
                    Settings.Layout.Options.SwapOnHotkeyFocused = layoutDef.SwapOnHotkeyFocused;
                    Settings.Layout.Options.LeaveHole = layoutDef.LeaveHole;
                    Settings.Layout.Options.AvoidTaskbar = layoutDef.AvoidTaskbar;
                    Settings.Layout.Options.RescaleWindows = layoutDef.RescaleWindows;
                }
            }

            Save();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to import JMB settings: {ex.Message}");
            return false;
        }
    }
}

#region Joe Multiboxer Settings DTOs

/// <summary>
/// Joe Multiboxer Basic Core settings file format
/// </summary>
internal class JoeMultiboxerSettings
{
    [JsonPropertyName("launcher")]
    public JmbLauncherSettings? Launcher { get; set; }

    [JsonPropertyName("highlighter")]
    public JmbHighlighterSettings? Highlighter { get; set; }

    [JsonPropertyName("hotkeys")]
    public JmbHotkeySettings? Hotkeys { get; set; }

    [JsonPropertyName("performance")]
    public JmbPerformanceSettings? Performance { get; set; }

    [JsonPropertyName("windowLayout")]
    public JmbWindowLayoutSettings? WindowLayout { get; set; }
}

internal class JmbLauncherSettings
{
    [JsonPropertyName("profiles")]
    public List<JmbLaunchProfile>? Profiles { get; set; }

    [JsonPropertyName("lastSelectedProfile")]
    public string? LastSelectedProfile { get; set; }
}

internal class JmbLaunchProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("game")]
    public string? Game { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("executable")]
    public string? Executable { get; set; }

    [JsonPropertyName("parameters")]
    public string? Parameters { get; set; }

    [JsonPropertyName("useDefaultVirtualFiles")]
    public bool UseDefaultVirtualFiles { get; set; }

    [JsonPropertyName("virtualFiles")]
    public List<JmbVirtualFile>? VirtualFiles { get; set; }
}

internal class JmbVirtualFile
{
    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("replacement")]
    public string? Replacement { get; set; }
}

internal class JmbHighlighterSettings
{
    [JsonPropertyName("showBorder")]
    public bool ShowBorder { get; set; }

    [JsonPropertyName("showNumber")]
    public bool ShowNumber { get; set; }
}

internal class JmbHotkeySettings
{
    [JsonPropertyName("slotHotkeys")]
    public List<string>? SlotHotkeys { get; set; }

    [JsonPropertyName("previousWindow")]
    public string? PreviousWindow { get; set; }

    [JsonPropertyName("nextWindow")]
    public string? NextWindow { get; set; }

    [JsonPropertyName("globalSwitchingHotkeys")]
    public bool GlobalSwitchingHotkeys { get; set; }
}

internal class JmbPerformanceSettings
{
    [JsonPropertyName("lockAffinity")]
    public bool LockAffinity { get; set; }

    [JsonPropertyName("background")]
    public JmbFpsSettings? Background { get; set; }

    [JsonPropertyName("foreground")]
    public JmbFpsSettings? Foreground { get; set; }
}

internal class JmbFpsSettings
{
    [JsonPropertyName("maxFPS")]
    public int MaxFPS { get; set; }

    [JsonPropertyName("calculate")]
    public bool Calculate { get; set; }
}

internal class JmbWindowLayoutSettings
{
    [JsonPropertyName("useLayout")]
    public string? UseLayout { get; set; }

    [JsonPropertyName("layouts")]
    public List<JmbWindowLayout>? Layouts { get; set; }
}

internal class JmbWindowLayout
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("style")]
    public string? Style { get; set; }

    [JsonPropertyName("swapOnActivate")]
    public bool SwapOnActivate { get; set; } = true;

    [JsonPropertyName("swapOnHotkeyFocused")]
    public bool SwapOnHotkeyFocused { get; set; } = true;

    [JsonPropertyName("leaveHole")]
    public bool LeaveHole { get; set; }

    [JsonPropertyName("avoidTaskbar")]
    public bool AvoidTaskbar { get; set; }

    [JsonPropertyName("rescaleWindows")]
    public bool RescaleWindows { get; set; } = true;

    [JsonPropertyName("mainRegion")]
    public JmbWindowRegion? MainRegion { get; set; }

    [JsonPropertyName("regions")]
    public List<JmbWindowRegion>? Regions { get; set; }
}

internal class JmbWindowRegion
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

#endregion
