using System.Text.Json.Serialization;
using Multiboxer.Core.Layout;

namespace Multiboxer.Core.Config;

/// <summary>
/// A saved window layout preset with fore/back regions per slot
/// </summary>
public class SavedWindowLayout
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Layout";

    [JsonPropertyName("slotRegions")]
    public List<SlotRegion> SlotRegions { get; set; } = new();
}

/// <summary>
/// Main application settings
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Settings file version
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Launch profiles
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<LaunchProfile> Profiles { get; set; } = new();

    /// <summary>
    /// Hotkey settings
    /// </summary>
    [JsonPropertyName("hotkeys")]
    public HotkeySettings? Hotkeys { get; set; }

    /// <summary>
    /// Layout settings
    /// </summary>
    [JsonPropertyName("layout")]
    public LayoutSettings? Layout { get; set; }

    /// <summary>
    /// Highlighter settings
    /// </summary>
    [JsonPropertyName("highlighter")]
    public HighlighterSettings? Highlighter { get; set; }

    /// <summary>
    /// Whether to apply custom window titles to managed game windows
    /// </summary>
    [JsonPropertyName("renameWindows")]
    public bool RenameWindows { get; set; } = true;

    /// <summary>
    /// Performance settings
    /// </summary>
    [JsonPropertyName("performance")]
    public PerformanceSettings? Performance { get; set; }

    /// <summary>
    /// Window position and size
    /// </summary>
    [JsonPropertyName("window")]
    public WindowSettings? Window { get; set; }

    /// <summary>
    /// Saved window layout presets
    /// </summary>
    [JsonPropertyName("savedWindowLayouts")]
    public List<SavedWindowLayout> SavedWindowLayouts { get; set; } = new();

    /// <summary>
    /// Name of the active saved window layout (auto-applied on startup when slots are assigned)
    /// </summary>
    [JsonPropertyName("activeSavedLayoutName")]
    public string? ActiveSavedLayoutName { get; set; }

    /// <summary>
    /// Create default settings
    /// </summary>
    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Version = "1.0",
            Profiles = new List<LaunchProfile>(),
            Hotkeys = HotkeySettings.CreateDefault(),
            Layout = LayoutSettings.CreateDefault(),
            Highlighter = HighlighterSettings.CreateDefault(),
            Performance = PerformanceSettings.CreateDefault(),
            Window = new WindowSettings()
        };
    }
}

/// <summary>
/// Hotkey configuration
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// Slot hotkeys (F1-F12 by default)
    /// </summary>
    [JsonPropertyName("slotHotkeys")]
    public List<string> SlotHotkeys { get; set; } = new();

    /// <summary>
    /// Previous window hotkey
    /// </summary>
    [JsonPropertyName("previousWindow")]
    public string PreviousWindow { get; set; } = "Ctrl+Alt+Z";

    /// <summary>
    /// Next window hotkey
    /// </summary>
    [JsonPropertyName("nextWindow")]
    public string NextWindow { get; set; } = "Ctrl+Alt+X";

    /// <summary>
    /// Whether hotkeys are enabled globally
    /// </summary>
    [JsonPropertyName("globalHotkeysEnabled")]
    public bool GlobalHotkeysEnabled { get; set; } = true;

    public static HotkeySettings CreateDefault()
    {
        return new HotkeySettings
        {
            SlotHotkeys = new List<string>
            {
                "F1", "F2", "F3", "F4", "F5", "F6",
                "F7", "F8", "F9", "F10", "F11", "F12", "End"
            },
            PreviousWindow = "Ctrl+Alt+Z",
            NextWindow = "Ctrl+Alt+X",
            GlobalHotkeysEnabled = true
        };
    }
}

/// <summary>
/// Layout configuration
/// </summary>
public class LayoutSettings
{
    /// <summary>
    /// Active layout name
    /// </summary>
    [JsonPropertyName("activeLayout")]
    public string ActiveLayout { get; set; } = "Horizontal";

    /// <summary>
    /// Layout options
    /// </summary>
    [JsonPropertyName("options")]
    public LayoutOptions Options { get; set; } = new();

    /// <summary>
    /// Custom layout definitions
    /// </summary>
    [JsonPropertyName("customLayouts")]
    public List<CustomLayout> CustomLayouts { get; set; } = new();

    public static LayoutSettings CreateDefault()
    {
        return new LayoutSettings
        {
            ActiveLayout = "Horizontal",
            Options = new LayoutOptions
            {
                SwapOnActivate = true,
                SwapOnHotkeyFocused = true,
                LeaveHole = false,
                AvoidTaskbar = true,
                MakeBorderless = true,
                RescaleWindows = true
            },
            CustomLayouts = new List<CustomLayout>()
        };
    }
}

/// <summary>
/// Highlighter/overlay settings
/// </summary>
public class HighlighterSettings
{
    /// <summary>
    /// Show border on active window
    /// </summary>
    [JsonPropertyName("showBorder")]
    public bool ShowBorder { get; set; } = true;

    /// <summary>
    /// Show slot number on background windows
    /// </summary>
    [JsonPropertyName("showNumber")]
    public bool ShowNumber { get; set; } = true;

    /// <summary>
    /// Border color (hex)
    /// </summary>
    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; } = "#FF0000";

    /// <summary>
    /// Border thickness
    /// </summary>
    [JsonPropertyName("borderThickness")]
    public double BorderThickness { get; set; } = 3;

    public static HighlighterSettings CreateDefault()
    {
        return new HighlighterSettings
        {
            ShowBorder = true,
            ShowNumber = true,
            BorderColor = "#FF0000",
            BorderThickness = 3
        };
    }
}

/// <summary>
/// Performance settings
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// Lock process CPU affinity
    /// </summary>
    [JsonPropertyName("lockAffinity")]
    public bool LockAffinity { get; set; }

    /// <summary>
    /// Background window FPS limit (0 = unlimited)
    /// </summary>
    [JsonPropertyName("backgroundMaxFps")]
    public int BackgroundMaxFps { get; set; } = 30;

    /// <summary>
    /// Foreground window FPS limit (0 = unlimited)
    /// </summary>
    [JsonPropertyName("foregroundMaxFps")]
    public int ForegroundMaxFps { get; set; }

    public static PerformanceSettings CreateDefault()
    {
        return new PerformanceSettings
        {
            LockAffinity = false,
            BackgroundMaxFps = 30,
            ForegroundMaxFps = 0
        };
    }
}

/// <summary>
/// Window position settings
/// </summary>
public class WindowSettings
{
    [JsonPropertyName("left")]
    public double Left { get; set; }

    [JsonPropertyName("top")]
    public double Top { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; } = 900;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 600;

    [JsonPropertyName("maximized")]
    public bool Maximized { get; set; }

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; }
}
