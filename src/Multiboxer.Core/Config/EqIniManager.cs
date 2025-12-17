using System.Text;
using System.Text.RegularExpressions;

namespace Multiboxer.Core.Config;

/// <summary>
/// Manages EverQuest eqclient.ini files for per-slot window dimensions.
/// Creates slot-specific INI files with correct window sizes before launch.
/// </summary>
public class EqIniManager
{
    private readonly string _eqPath;

    public EqIniManager(string eqPath)
    {
        _eqPath = eqPath;
    }

    /// <summary>
    /// Get the path to the base eqclient.ini
    /// </summary>
    public string BaseIniPath => Path.Combine(_eqPath, "eqclient.ini");

    /// <summary>
    /// Get the path to a slot-specific INI file
    /// </summary>
    public string GetSlotIniPath(int slotId)
    {
        return Path.Combine(_eqPath, $"eqclient.slot{slotId}.ini");
    }

    /// <summary>
    /// Create a slot-specific INI file with the specified window dimensions
    /// </summary>
    public bool CreateSlotIni(int slotId, int windowedWidth, int windowedHeight, int windowedX, int windowedY)
    {
        try
        {
            string baseIniPath = BaseIniPath;
            if (!File.Exists(baseIniPath))
            {
                return false;
            }

            string content = File.ReadAllText(baseIniPath);

            // Update window dimensions
            content = SetIniValue(content, "WindowedWidth", windowedWidth.ToString());
            content = SetIniValue(content, "WindowedHeight", windowedHeight.ToString());
            content = SetIniValue(content, "WindowedModeXOffset", windowedX.ToString());
            content = SetIniValue(content, "WindowedModeYOffset", windowedY.ToString());

            // Ensure windowed mode
            content = SetIniValue(content, "WindowedMode", "TRUE");

            string slotIniPath = GetSlotIniPath(slotId);
            File.WriteAllText(slotIniPath, content);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update an existing INI file's window dimensions
    /// </summary>
    public bool UpdateWindowDimensions(string iniPath, int windowedWidth, int windowedHeight, int windowedX, int windowedY)
    {
        try
        {
            if (!File.Exists(iniPath))
            {
                return false;
            }

            string content = File.ReadAllText(iniPath);

            content = SetIniValue(content, "WindowedWidth", windowedWidth.ToString());
            content = SetIniValue(content, "WindowedHeight", windowedHeight.ToString());
            content = SetIniValue(content, "WindowedModeXOffset", windowedX.ToString());
            content = SetIniValue(content, "WindowedModeYOffset", windowedY.ToString());

            File.WriteAllText(iniPath, content);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Read current window dimensions from an INI file
    /// </summary>
    public (int width, int height, int x, int y)? GetWindowDimensions(string iniPath)
    {
        try
        {
            if (!File.Exists(iniPath))
            {
                return null;
            }

            string content = File.ReadAllText(iniPath);

            int width = GetIniValueInt(content, "WindowedWidth", 1024);
            int height = GetIniValueInt(content, "WindowedHeight", 768);
            int x = GetIniValueInt(content, "WindowedModeXOffset", 0);
            int y = GetIniValueInt(content, "WindowedModeYOffset", 0);

            return (width, height, x, y);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Set a value in INI content
    /// </summary>
    private static string SetIniValue(string content, string key, string value)
    {
        var pattern = new Regex($@"^{Regex.Escape(key)}=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        if (pattern.IsMatch(content))
        {
            return pattern.Replace(content, $"{key}={value}");
        }
        else
        {
            // Add the key if it doesn't exist (add to Defaults section or end)
            var defaultsMatch = Regex.Match(content, @"\[Defaults\]", RegexOptions.IgnoreCase);
            if (defaultsMatch.Success)
            {
                int insertPos = defaultsMatch.Index + defaultsMatch.Length;
                return content.Insert(insertPos, $"\r\n{key}={value}");
            }
            else
            {
                return content + $"\r\n{key}={value}";
            }
        }
    }

    /// <summary>
    /// Get an integer value from INI content
    /// </summary>
    private static int GetIniValueInt(string content, string key, int defaultValue)
    {
        var pattern = new Regex($@"^{Regex.Escape(key)}=(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var match = pattern.Match(content);

        if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
        {
            return value;
        }

        return defaultValue;
    }

    /// <summary>
    /// Clean up slot-specific INI files
    /// </summary>
    public void CleanupSlotInis()
    {
        try
        {
            var slotInis = Directory.GetFiles(_eqPath, "eqclient.slot*.ini");
            foreach (var ini in slotInis)
            {
                File.Delete(ini);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Prepare all slot INI files based on layout regions
    /// </summary>
    public void PrepareSlotInis(IEnumerable<(int slotId, int width, int height, int x, int y)> slotDimensions)
    {
        foreach (var (slotId, width, height, x, y) in slotDimensions)
        {
            CreateSlotIni(slotId, width, height, x, y);
        }
    }
}
