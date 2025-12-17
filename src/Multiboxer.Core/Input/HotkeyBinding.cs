using System.Text.Json.Serialization;
using Multiboxer.Native;

namespace Multiboxer.Core.Input;

/// <summary>
/// Represents a hotkey binding configuration
/// </summary>
public class HotkeyBinding
{
    /// <summary>
    /// Unique identifier for this binding
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Action name (e.g., "slot1", "nextWindow", "previousWindow")
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Virtual key code
    /// </summary>
    [JsonPropertyName("key")]
    public uint KeyCode { get; set; }

    /// <summary>
    /// Key name for display
    /// </summary>
    [JsonPropertyName("keyName")]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Modifier keys (Ctrl, Alt, Shift, Win)
    /// </summary>
    [JsonPropertyName("modifiers")]
    public ModifierKeys Modifiers { get; set; }

    /// <summary>
    /// Whether this hotkey is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this is a global hotkey (works even when app not focused)
    /// </summary>
    [JsonPropertyName("global")]
    public bool IsGlobal { get; set; } = true;

    /// <summary>
    /// Associated slot ID for slot hotkeys
    /// </summary>
    [JsonPropertyName("slotId")]
    public int? SlotId { get; set; }

    /// <summary>
    /// Get display string for this hotkey
    /// </summary>
    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Win))
                parts.Add("Win");

            parts.Add(KeyName);

            return string.Join("+", parts);
        }
    }

    /// <summary>
    /// Create a slot hotkey binding
    /// </summary>
    public static HotkeyBinding CreateSlotHotkey(int slotId, uint keyCode, string keyName, ModifierKeys modifiers = ModifierKeys.None)
    {
        return new HotkeyBinding
        {
            Id = slotId,
            Action = $"slot{slotId}",
            KeyCode = keyCode,
            KeyName = keyName,
            Modifiers = modifiers,
            SlotId = slotId,
            IsGlobal = true
        };
    }

    /// <summary>
    /// Create a navigation hotkey binding
    /// </summary>
    public static HotkeyBinding CreateNavigationHotkey(int id, string action, uint keyCode, string keyName, ModifierKeys modifiers)
    {
        return new HotkeyBinding
        {
            Id = id,
            Action = action,
            KeyCode = keyCode,
            KeyName = keyName,
            Modifiers = modifiers,
            IsGlobal = true
        };
    }
}

/// <summary>
/// Virtual key codes for common keys
/// </summary>
public static class VirtualKeys
{
    // Function keys
    public const uint VK_F1 = 0x70;
    public const uint VK_F2 = 0x71;
    public const uint VK_F3 = 0x72;
    public const uint VK_F4 = 0x73;
    public const uint VK_F5 = 0x74;
    public const uint VK_F6 = 0x75;
    public const uint VK_F7 = 0x76;
    public const uint VK_F8 = 0x77;
    public const uint VK_F9 = 0x78;
    public const uint VK_F10 = 0x79;
    public const uint VK_F11 = 0x7A;
    public const uint VK_F12 = 0x7B;

    // Number keys
    public const uint VK_0 = 0x30;
    public const uint VK_1 = 0x31;
    public const uint VK_2 = 0x32;
    public const uint VK_3 = 0x33;
    public const uint VK_4 = 0x34;
    public const uint VK_5 = 0x35;
    public const uint VK_6 = 0x36;
    public const uint VK_7 = 0x37;
    public const uint VK_8 = 0x38;
    public const uint VK_9 = 0x39;

    // Numpad
    public const uint VK_NUMPAD0 = 0x60;
    public const uint VK_NUMPAD1 = 0x61;
    public const uint VK_NUMPAD2 = 0x62;
    public const uint VK_NUMPAD3 = 0x63;
    public const uint VK_NUMPAD4 = 0x64;
    public const uint VK_NUMPAD5 = 0x65;
    public const uint VK_NUMPAD6 = 0x66;
    public const uint VK_NUMPAD7 = 0x67;
    public const uint VK_NUMPAD8 = 0x68;
    public const uint VK_NUMPAD9 = 0x69;

    // Navigation
    public const uint VK_END = 0x23;
    public const uint VK_HOME = 0x24;
    public const uint VK_INSERT = 0x2D;
    public const uint VK_DELETE = 0x2E;
    public const uint VK_PRIOR = 0x21; // Page Up
    public const uint VK_NEXT = 0x22;  // Page Down

    // Arrow keys
    public const uint VK_LEFT = 0x25;
    public const uint VK_UP = 0x26;
    public const uint VK_RIGHT = 0x27;
    public const uint VK_DOWN = 0x28;

    // Letters
    public const uint VK_A = 0x41;
    public const uint VK_B = 0x42;
    public const uint VK_C = 0x43;
    public const uint VK_D = 0x44;
    public const uint VK_E = 0x45;
    public const uint VK_F = 0x46;
    public const uint VK_G = 0x47;
    public const uint VK_H = 0x48;
    public const uint VK_I = 0x49;
    public const uint VK_J = 0x4A;
    public const uint VK_K = 0x4B;
    public const uint VK_L = 0x4C;
    public const uint VK_M = 0x4D;
    public const uint VK_N = 0x4E;
    public const uint VK_O = 0x4F;
    public const uint VK_P = 0x50;
    public const uint VK_Q = 0x51;
    public const uint VK_R = 0x52;
    public const uint VK_S = 0x53;
    public const uint VK_T = 0x54;
    public const uint VK_U = 0x55;
    public const uint VK_V = 0x56;
    public const uint VK_W = 0x57;
    public const uint VK_X = 0x58;
    public const uint VK_Y = 0x59;
    public const uint VK_Z = 0x5A;

    // Special keys
    public const uint VK_TAB = 0x09;
    public const uint VK_SPACE = 0x20;
    public const uint VK_ESCAPE = 0x1B;
    public const uint VK_RETURN = 0x0D;
    public const uint VK_BACK = 0x08;

    /// <summary>
    /// Get the key name from a virtual key code
    /// </summary>
    public static string GetKeyName(uint vkCode)
    {
        return vkCode switch
        {
            >= VK_F1 and <= VK_F12 => $"F{vkCode - VK_F1 + 1}",
            >= VK_0 and <= VK_9 => ((char)vkCode).ToString(),
            >= VK_A and <= VK_Z => ((char)vkCode).ToString(),
            >= VK_NUMPAD0 and <= VK_NUMPAD9 => $"Num{vkCode - VK_NUMPAD0}",
            VK_END => "End",
            VK_HOME => "Home",
            VK_INSERT => "Insert",
            VK_DELETE => "Delete",
            VK_PRIOR => "PageUp",
            VK_NEXT => "PageDown",
            VK_LEFT => "Left",
            VK_UP => "Up",
            VK_RIGHT => "Right",
            VK_DOWN => "Down",
            VK_TAB => "Tab",
            VK_SPACE => "Space",
            VK_ESCAPE => "Escape",
            VK_RETURN => "Enter",
            VK_BACK => "Backspace",
            _ => $"Key{vkCode:X2}"
        };
    }
}
