using System.Text.Json.Serialization;

namespace Multiboxer.Core.Config;

/// <summary>
/// Configuration for launching a game
/// </summary>
public class LaunchProfile
{
    /// <summary>
    /// Display name of the profile
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Game identifier
    /// </summary>
    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    /// <summary>
    /// Path to the game directory
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Executable filename (launcher or game)
    /// </summary>
    [JsonPropertyName("executable")]
    public string Executable { get; set; } = string.Empty;

    /// <summary>
    /// Game executable filename (if different from launcher)
    /// For example, if Executable is "LaunchPad.exe", GameExecutable might be "eqgame.exe"
    /// Leave empty if Executable is the actual game
    /// </summary>
    [JsonPropertyName("gameExecutable")]
    public string? GameExecutable { get; set; }

    /// <summary>
    /// Command line arguments
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use virtual file redirection
    /// </summary>
    [JsonPropertyName("useVirtualFiles")]
    public bool UseVirtualFiles { get; set; }

    /// <summary>
    /// Virtual file mappings
    /// </summary>
    [JsonPropertyName("virtualFiles")]
    public List<VirtualFileMapping> VirtualFiles { get; set; } = new();

    /// <summary>
    /// Whether to run as administrator
    /// </summary>
    [JsonPropertyName("runAsAdmin")]
    public bool RunAsAdmin { get; set; }

    /// <summary>
    /// Delay in milliseconds before launching
    /// </summary>
    [JsonPropertyName("launchDelay")]
    public int LaunchDelay { get; set; }

    /// <summary>
    /// Window class name to look for (if different from main window)
    /// </summary>
    [JsonPropertyName("windowClass")]
    public string? WindowClass { get; set; }

    /// <summary>
    /// Expected window title pattern (regex)
    /// </summary>
    [JsonPropertyName("windowTitlePattern")]
    public string? WindowTitlePattern { get; set; }

    /// <summary>
    /// Optional custom window title to apply after launch/attach.
    /// If empty, a default "Slot {id}" is used when renaming is enabled.
    /// </summary>
    [JsonPropertyName("customWindowTitle")]
    public string? CustomWindowTitle { get; set; }

    /// <summary>
    /// Get the full path to the executable
    /// </summary>
    [JsonIgnore]
    public string FullExecutablePath => System.IO.Path.Combine(Path, Executable);

    /// <summary>
    /// Validate the profile configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(Path) &&
               !string.IsNullOrWhiteSpace(Executable) &&
               Directory.Exists(Path) &&
               File.Exists(FullExecutablePath);
    }
}

/// <summary>
/// Virtual file mapping for per-slot file redirection
/// </summary>
public class VirtualFileMapping
{
    /// <summary>
    /// Pattern to match (e.g., "*/eqclient.ini")
    /// </summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Replacement path (e.g., "{path}/eqclient.{slot}.ini")
    /// {slot} will be replaced with slot number
    /// {path} will be replaced with the game path
    /// </summary>
    [JsonPropertyName("replacement")]
    public string Replacement { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an exact match or pattern match
    /// </summary>
    [JsonPropertyName("exact")]
    public bool Exact { get; set; }

    /// <summary>
    /// Get the resolved replacement path for a specific slot
    /// </summary>
    public string GetReplacementForSlot(int slotId, string gamePath)
    {
        return Replacement
            .Replace("{slot}", slotId.ToString())
            .Replace("{path}", gamePath);
    }
}
