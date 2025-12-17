using System.Runtime.InteropServices;
using Multiboxer.Native;

namespace Multiboxer.Core.Input;

/// <summary>
/// Manages global hotkey registration and events
/// </summary>
public class HotkeyManager : IDisposable
{
    private readonly Dictionary<int, HotkeyBinding> _bindings = new();
    private readonly IntPtr _windowHandle;
    private readonly object _lock = new();
    private bool _disposed;
    private int _nextId = 1000;

    // Low-level keyboard hook for more reliable hotkey detection
    private IntPtr _hookHandle;
    private User32.LowLevelKeyboardProc? _hookProc;
    private bool _useLowLevelHook;

    /// <summary>
    /// Event raised when a hotkey is pressed
    /// </summary>
    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// All registered bindings
    /// </summary>
    public IReadOnlyDictionary<int, HotkeyBinding> Bindings => _bindings;

    /// <summary>
    /// Create a hotkey manager that uses RegisterHotKey
    /// </summary>
    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _useLowLevelHook = false;
    }

    /// <summary>
    /// Create a hotkey manager that uses low-level keyboard hook
    /// </summary>
    public HotkeyManager()
    {
        _windowHandle = IntPtr.Zero;
        _useLowLevelHook = true;
        InstallLowLevelHook();
    }

    /// <summary>
    /// Register a hotkey binding
    /// </summary>
    public bool RegisterHotkey(HotkeyBinding binding)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotkeyManager));

        lock (_lock)
        {
            // Assign ID if not set
            if (binding.Id == 0)
            {
                binding.Id = _nextId++;
            }

            // Unregister existing binding with same ID
            if (_bindings.ContainsKey(binding.Id))
            {
                UnregisterHotkey(binding.Id);
            }

            if (_useLowLevelHook)
            {
                // Low-level hook handles all keys, just track the binding
                _bindings[binding.Id] = binding;
                return true;
            }
            else
            {
                // Use RegisterHotKey API
                if (binding.IsGlobal && _windowHandle != IntPtr.Zero)
                {
                    bool success = User32.RegisterHotKey(
                        _windowHandle,
                        binding.Id,
                        binding.Modifiers | ModifierKeys.NoRepeat,
                        binding.KeyCode);

                    if (success)
                    {
                        _bindings[binding.Id] = binding;
                    }

                    return success;
                }
                else
                {
                    _bindings[binding.Id] = binding;
                    return true;
                }
            }
        }
    }

    /// <summary>
    /// Register default slot hotkeys (F1-F12 for slots 1-12)
    /// </summary>
    public void RegisterDefaultSlotHotkeys()
    {
        // F1-F12 for slots 1-12
        for (int i = 0; i < 12; i++)
        {
            RegisterHotkey(HotkeyBinding.CreateSlotHotkey(
                i + 1,
                VirtualKeys.VK_F1 + (uint)i,
                $"F{i + 1}"));
        }

        // End key for slot 13
        RegisterHotkey(HotkeyBinding.CreateSlotHotkey(13, VirtualKeys.VK_END, "End"));
    }

    /// <summary>
    /// Register default navigation hotkeys
    /// </summary>
    public void RegisterDefaultNavigationHotkeys()
    {
        // Ctrl+Alt+Z for previous window
        RegisterHotkey(HotkeyBinding.CreateNavigationHotkey(
            100,
            "previousWindow",
            VirtualKeys.VK_Z,
            "Z",
            ModifierKeys.Control | ModifierKeys.Alt));

        // Ctrl+Alt+X for next window
        RegisterHotkey(HotkeyBinding.CreateNavigationHotkey(
            101,
            "nextWindow",
            VirtualKeys.VK_X,
            "X",
            ModifierKeys.Control | ModifierKeys.Alt));
    }

    /// <summary>
    /// Unregister a hotkey by ID
    /// </summary>
    public bool UnregisterHotkey(int id)
    {
        lock (_lock)
        {
            if (!_bindings.TryGetValue(id, out var binding))
                return false;

            if (!_useLowLevelHook && _windowHandle != IntPtr.Zero && binding.IsGlobal)
            {
                User32.UnregisterHotKey(_windowHandle, id);
            }

            _bindings.Remove(id);
            return true;
        }
    }

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            foreach (var binding in _bindings.Values.ToList())
            {
                if (!_useLowLevelHook && _windowHandle != IntPtr.Zero && binding.IsGlobal)
                {
                    User32.UnregisterHotKey(_windowHandle, binding.Id);
                }
            }
            _bindings.Clear();
        }
    }

    /// <summary>
    /// Process WM_HOTKEY message (call from window procedure)
    /// </summary>
    public void ProcessHotkeyMessage(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (_bindings.TryGetValue(id, out var binding) && binding.Enabled)
        {
            OnHotkeyPressed(binding);
        }
    }

    /// <summary>
    /// Get a binding by action name
    /// </summary>
    public HotkeyBinding? GetBindingByAction(string action)
    {
        return _bindings.Values.FirstOrDefault(b =>
            b.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get bindings for a specific slot
    /// </summary>
    public HotkeyBinding? GetSlotBinding(int slotId)
    {
        return _bindings.Values.FirstOrDefault(b => b.SlotId == slotId);
    }

    /// <summary>
    /// Update a binding's key
    /// </summary>
    public bool UpdateBinding(int id, uint keyCode, string keyName, ModifierKeys modifiers)
    {
        lock (_lock)
        {
            if (!_bindings.TryGetValue(id, out var binding))
                return false;

            // Unregister old
            if (!_useLowLevelHook && _windowHandle != IntPtr.Zero && binding.IsGlobal)
            {
                User32.UnregisterHotKey(_windowHandle, id);
            }

            // Update
            binding.KeyCode = keyCode;
            binding.KeyName = keyName;
            binding.Modifiers = modifiers;

            // Re-register
            if (!_useLowLevelHook && _windowHandle != IntPtr.Zero && binding.IsGlobal)
            {
                return User32.RegisterHotKey(_windowHandle, id, modifiers | ModifierKeys.NoRepeat, keyCode);
            }

            return true;
        }
    }

    private void InstallLowLevelHook()
    {
        _hookProc = LowLevelKeyboardProc;
        var moduleHandle = Kernel32.GetModuleHandle(null);
        _hookHandle = User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
    }

    private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == User32.WM_KEYDOWN || msg == User32.WM_SYSKEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var keyCode = hookStruct.vkCode;

                // Get current modifier state
                var modifiers = GetCurrentModifiers();

                // Check for matching binding
                foreach (var binding in _bindings.Values)
                {
                    if (binding.Enabled &&
                        binding.KeyCode == keyCode &&
                        binding.Modifiers == modifiers)
                    {
                        OnHotkeyPressed(binding);
                        // Don't block the key, let it pass through
                    }
                }
            }
        }

        return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static ModifierKeys GetCurrentModifiers()
    {
        var modifiers = ModifierKeys.None;

        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) // VK_CONTROL
            modifiers |= ModifierKeys.Control;
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) // VK_MENU (Alt)
            modifiers |= ModifierKeys.Alt;
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) // VK_SHIFT
            modifiers |= ModifierKeys.Shift;
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) // VK_LWIN/VK_RWIN
            modifiers |= ModifierKeys.Win;

        return modifiers;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void OnHotkeyPressed(HotkeyBinding binding)
    {
        HotkeyPressed?.Invoke(this, new HotkeyEventArgs(binding));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        UnregisterAll();

        if (_hookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for hotkey events
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    public HotkeyBinding Binding { get; }
    public string Action => Binding.Action;
    public int? SlotId => Binding.SlotId;

    public HotkeyEventArgs(HotkeyBinding binding)
    {
        Binding = binding;
    }
}
