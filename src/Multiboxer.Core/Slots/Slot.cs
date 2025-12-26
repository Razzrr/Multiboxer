using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Multiboxer.Core.Config;
using Multiboxer.Core.Logging;
using Multiboxer.Core.Window;

namespace Multiboxer.Core.Slots;

/// <summary>
/// Represents a single multiboxing slot containing a game process
/// </summary>
public partial class Slot : ObservableObject, IDisposable
{
    private Process? _process;
    private bool _disposed;

    /// <summary>
    /// Global registry of window handles that have been claimed by slots.
    /// This prevents multiple slots from grabbing the same window during batch launches.
    /// </summary>
    private static readonly HashSet<IntPtr> _claimedWindows = new();
    private static readonly object _claimLock = new();

    /// <summary>
    /// Claim a window handle for this slot (prevents other slots from using it)
    /// </summary>
    private static bool TryClaimWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        lock (_claimLock)
        {
            if (_claimedWindows.Contains(hWnd))
            {
                Debug.WriteLine($"  Window 0x{hWnd:X} already claimed by another slot");
                return false;
            }
            _claimedWindows.Add(hWnd);
            Debug.WriteLine($"  Claimed window 0x{hWnd:X}");
            return true;
        }
    }

    /// <summary>
    /// Release a previously claimed window handle
    /// </summary>
    private static void ReleaseWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        lock (_claimLock)
        {
            _claimedWindows.Remove(hWnd);
            Debug.WriteLine($"  Released window 0x{hWnd:X}");
        }
    }

    /// <summary>
    /// Check if a window is already claimed by another slot
    /// </summary>
    private static bool IsWindowClaimed(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        lock (_claimLock)
        {
            return _claimedWindows.Contains(hWnd);
        }
    }

    /// <summary>
    /// Unique identifier for this slot (1-40)
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// The process running in this slot
    /// </summary>
    public Process? Process
    {
        get => _process;
        private set
        {
            if (_process != value)
            {
                // Unsubscribe from old process
                if (_process != null)
                {
                    _process.EnableRaisingEvents = false;
                    _process.Exited -= OnProcessExited;
                }

                _process = value;

                // Subscribe to new process
                if (_process != null)
                {
                    _process.EnableRaisingEvents = true;
                    _process.Exited += OnProcessExited;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(ProcessId));
                OnPropertyChanged(nameof(HasProcess));
            }
        }
    }

    /// <summary>
    /// The main window handle of the process
    /// </summary>
    [ObservableProperty]
    private IntPtr _mainWindowHandle;

    /// <summary>
    /// Current state of the slot
    /// </summary>
    [ObservableProperty]
    private SlotState _state = SlotState.Empty;

    /// <summary>
    /// Name of the launch profile used
    /// </summary>
    [ObservableProperty]
    private string _profileName = string.Empty;

    /// <summary>
    /// Display name for the slot (e.g., character name)
    /// </summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    /// <summary>
    /// Window title of the process
    /// </summary>
    [ObservableProperty]
    private string _windowTitle = string.Empty;

    /// <summary>
    /// Current window position X
    /// </summary>
    [ObservableProperty]
    private int _windowX;

    /// <summary>
    /// Current window position Y
    /// </summary>
    [ObservableProperty]
    private int _windowY;

    /// <summary>
    /// Current window width
    /// </summary>
    [ObservableProperty]
    private int _windowWidth;

    /// <summary>
    /// Current window height
    /// </summary>
    [ObservableProperty]
    private int _windowHeight;

    /// <summary>
    /// Whether the slot is currently in a loading/zoning state
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Timestamp of last loading state change
    /// </summary>
    private DateTime _lastLoadingChange = DateTime.MinValue;

    /// <summary>
    /// Previous window title for loading detection
    /// </summary>
    private string _previousWindowTitle = string.Empty;

    /// <summary>
    /// Previous window rect for stability detection
    /// </summary>
    private Native.RECT _previousWindowRect;

    /// <summary>
    /// Count of consecutive rect stability checks
    /// </summary>
    private int _rectStabilityCount;

    /// <summary>
    /// Loading detection keywords in window titles
    /// </summary>
    private static readonly string[] LoadingKeywords = new[]
    {
        "loading", "zoning", "please wait", "connecting",
        "entering", "Loading...", "EverQuest"  // EQ shows just "EverQuest" during zone
    };

    /// <summary>
    /// Whether this slot has a running process
    /// </summary>
    public bool HasProcess => _process != null && !_process.HasExited;

    /// <summary>
    /// Process ID if running
    /// </summary>
    public int? ProcessId => _process?.Id;

    /// <summary>
    /// Event raised when the slot's process exits
    /// </summary>
    public event EventHandler<SlotEventArgs>? ProcessExited;

    /// <summary>
    /// Event raised when the slot's state changes
    /// </summary>
    public event EventHandler<SlotEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when the slot's window changes
    /// </summary>
    public event EventHandler<SlotEventArgs>? WindowChanged;

    public Slot(int id)
    {
        if (id < 1 || id > SlotManager.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(id), $"Slot ID must be between 1 and {SlotManager.MaxSlots}");

        Id = id;
        DisplayName = $"Slot {id}";
    }

    /// <summary>
    /// Launch a process into this slot
    /// </summary>
    public async Task<bool> LaunchAsync(LaunchProfile profile, CancellationToken cancellationToken = default)
    {
        if (HasProcess)
        {
            throw new InvalidOperationException($"Slot {Id} already has a running process");
        }

        State = SlotState.Starting;
        ProfileName = profile.Name;
        _launchProfile = profile;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(profile.Path, profile.Executable),
                Arguments = profile.Arguments,
                WorkingDirectory = profile.Path,
                UseShellExecute = true
            };

            // Run as admin if needed
            if (profile.RunAsAdmin)
            {
                startInfo.Verb = "runas";
            }

            Process = Process.Start(startInfo);

            if (Process == null)
            {
                State = SlotState.Error;
                return false;
            }

            // Wait for the main window to become available
            // This uses enhanced detection that handles launchers and delayed window creation
            await WaitForMainWindowAsync(profile, cancellationToken);

            if (MainWindowHandle != IntPtr.Zero)
            {
                State = SlotState.Running;
                UpdateWindowInfo();
                return true;
            }
            else
            {
                State = SlotState.Error;
                return false;
            }
        }
        catch (Exception)
        {
            State = SlotState.Error;
            throw;
        }
    }

    // Store the launch profile for window detection
    private LaunchProfile? _launchProfile;

    /// <summary>
    /// Attach an existing process to this slot
    /// </summary>
    public void AttachProcess(Process process)
    {
        if (HasProcess)
        {
            throw new InvalidOperationException($"Slot {Id} already has a running process");
        }

        Process = process;
        MainWindowHandle = process.MainWindowHandle;
        State = MainWindowHandle != IntPtr.Zero ? SlotState.Running : SlotState.Starting;
        UpdateWindowInfo();
    }

    /// <summary>
    /// Detach the process from this slot without terminating it
    /// </summary>
    public void DetachProcess()
    {
        Process = null;
        MainWindowHandle = IntPtr.Zero;
        State = SlotState.Empty;
        ProfileName = string.Empty;
        WindowTitle = string.Empty;
    }

    /// <summary>
    /// Close the process in this slot
    /// </summary>
    public void Close()
    {
        if (Process != null && !Process.HasExited)
        {
            try
            {
                Process.CloseMainWindow();

                // Give it a moment to close gracefully
                if (!Process.WaitForExit(3000))
                {
                    Process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }

        DetachProcess();
    }

    /// <summary>
    /// Bring this slot's window to the foreground
    /// </summary>
    public bool Focus()
    {
        if (MainWindowHandle == IntPtr.Zero)
            return false;

        // Use the Window manager to properly focus the window
        // This handles the Windows restrictions on SetForegroundWindow
        return WindowHelper.ForceForegroundWindow(MainWindowHandle);
    }

    /// <summary>
    /// Update the cached window information
    /// </summary>
    public void UpdateWindowInfo()
    {
        if (MainWindowHandle == IntPtr.Zero)
            return;

        if (Native.User32.GetWindowRect(MainWindowHandle, out var rect))
        {
            WindowX = rect.Left;
            WindowY = rect.Top;
            WindowWidth = rect.Width;
            WindowHeight = rect.Height;
        }

        // Update window title
        if (Process != null)
        {
            try
            {
                Process.Refresh();
                WindowTitle = Process.MainWindowTitle;
            }
            catch
            {
                // Process may have exited
            }
        }
    }

    /// <summary>
    /// Check and update loading/zoning state.
    /// Call this periodically (e.g., every 100-200ms) during active usage.
    /// </summary>
    /// <returns>True if the loading state changed</returns>
    public bool UpdateLoadingState()
    {
        if (MainWindowHandle == IntPtr.Zero || Process == null || Process.HasExited)
        {
            if (IsLoading)
            {
                SetLoadingState(false, "no window");
                return true;
            }
            return false;
        }

        // Heuristic 1: Check window title for loading keywords
        string currentTitle = WindowTitle;
        bool titleIndicatesLoading = false;

        if (!string.IsNullOrEmpty(currentTitle))
        {
            foreach (var keyword in LoadingKeywords)
            {
                if (currentTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: "EverQuest" alone usually means loading
                    // but "EverQuest - CharName" means loaded
                    if (keyword == "EverQuest" && currentTitle.Contains(" - "))
                        continue;

                    titleIndicatesLoading = true;
                    break;
                }
            }
        }

        // Heuristic 2: Check window rect stability
        // During loading, the window rect may be unstable or minimized
        bool rectStable = true;
        if (Native.User32.GetWindowRect(MainWindowHandle, out var currentRect))
        {
            bool rectsMatch = currentRect.Left == _previousWindowRect.Left &&
                             currentRect.Top == _previousWindowRect.Top &&
                             currentRect.Width == _previousWindowRect.Width &&
                             currentRect.Height == _previousWindowRect.Height;

            if (rectsMatch)
            {
                _rectStabilityCount++;
            }
            else
            {
                _rectStabilityCount = 0;
            }

            // Consider rect stable if it's been the same for 3+ checks
            rectStable = _rectStabilityCount >= 3;
            _previousWindowRect = currentRect;

            // Zero-sized or minimized window suggests loading
            if (currentRect.Width <= 0 || currentRect.Height <= 0)
            {
                titleIndicatesLoading = true;
            }
        }

        // Heuristic 3: Title changed significantly (zone transition)
        bool titleChanged = !string.IsNullOrEmpty(_previousWindowTitle) &&
                           !string.IsNullOrEmpty(currentTitle) &&
                           _previousWindowTitle != currentTitle;

        _previousWindowTitle = currentTitle;

        // Combine heuristics
        bool nowLoading = titleIndicatesLoading || (!rectStable && titleChanged);

        // Debounce: require loading state to persist for a bit before reporting
        if (nowLoading != IsLoading)
        {
            // Only transition to loading immediately, but require stability to exit loading
            if (nowLoading)
            {
                SetLoadingState(true, $"title={titleIndicatesLoading}, rectStable={rectStable}");
                return true;
            }
            else if (rectStable && _rectStabilityCount >= 5)
            {
                // Require more stability before exiting loading state
                SetLoadingState(false, "rect stabilized");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Set loading state with logging
    /// </summary>
    private void SetLoadingState(bool loading, string reason)
    {
        bool wasLoading = IsLoading;
        IsLoading = loading;
        _lastLoadingChange = DateTime.Now;

        DebugLog.LoadingStateChanged(Id, wasLoading, loading);
        DebugLog.LoadingDetection(Id, loading, WindowTitle, _rectStabilityCount >= 3, false);
    }

    /// <summary>
    /// Force exit loading state (e.g., after user interaction confirms window is responsive)
    /// </summary>
    public void ClearLoadingState()
    {
        if (IsLoading)
        {
            SetLoadingState(false, "manually cleared");
        }
    }

    /// <summary>
    /// Check if this slot's window is currently in the foreground
    /// </summary>
    public bool IsForeground()
    {
        if (MainWindowHandle == IntPtr.Zero)
            return false;

        return Native.User32.GetForegroundWindow() == MainWindowHandle;
    }

    /// <summary>
    /// Refresh the main window handle (useful if the game creates a new window)
    /// </summary>
    public void RefreshWindowHandle()
    {
        if (Process == null || Process.HasExited)
            return;

        Process.Refresh();
        var newHandle = Process.MainWindowHandle;

        if (newHandle != MainWindowHandle && newHandle != IntPtr.Zero)
        {
            MainWindowHandle = newHandle;
            UpdateWindowInfo();
            WindowChanged?.Invoke(this, new SlotEventArgs(this));
        }
    }

    private async Task WaitForMainWindowAsync(LaunchProfile profile, CancellationToken cancellationToken)
    {
        if (Process == null)
            return;

        Debug.WriteLine($"Slot {Id}: WaitForMainWindowAsync starting, process {Process.Id}");

        // Configuration for window detection
        const int maxAttempts = 300;  // 30 seconds total
        const int delayMs = 100;

        // Track the initial process ID - the launcher might spawn a child process
        var initialProcessId = Process.Id;
        var targetProcessIds = new HashSet<int> { initialProcessId };

        for (int i = 0; i < maxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // First, try the simple approach - check if the process has a main window
            Process.Refresh();
            if (Process.MainWindowHandle != IntPtr.Zero)
            {
                var candidateWindow = Process.MainWindowHandle;
                // Verify this window matches our criteria AND is not already claimed
                if (IsWindowMatch(candidateWindow, profile) && !IsWindowClaimed(candidateWindow))
                {
                    if (TryClaimWindow(candidateWindow))
                    {
                        Debug.WriteLine($"Slot {Id}: Claimed window 0x{candidateWindow:X} from process {Process.Id}");
                        MainWindowHandle = candidateWindow;
                        return;
                    }
                }
            }

            // If the original process exited, it might be a launcher - look for child processes
            if (Process.HasExited)
            {
                var childProcess = await FindChildProcessAsync(initialProcessId, profile, cancellationToken);
                if (childProcess != null)
                {
                    // Switch to tracking the child process
                    Process = childProcess;
                    targetProcessIds.Add(childProcess.Id);
                    continue;
                }
            }

            // Try to find a matching window by enumerating all windows from our tracked processes
            // This will only return unclaimed windows
            var matchingWindow = FindMatchingWindow(targetProcessIds, profile);
            if (matchingWindow != IntPtr.Zero)
            {
                if (TryClaimWindow(matchingWindow))
                {
                    Debug.WriteLine($"Slot {Id}: Claimed window 0x{matchingWindow:X} via FindMatchingWindow");
                    MainWindowHandle = matchingWindow;

                    // If we found a window but it's from a different process, update our tracked process
                    Native.User32.GetWindowThreadProcessId(matchingWindow, out var windowProcessId);
                    if (windowProcessId != 0 && windowProcessId != Process.Id)
                    {
                        try
                        {
                            var newProcess = System.Diagnostics.Process.GetProcessById((int)windowProcessId);
                            Process = newProcess;
                        }
                        catch { /* Process may have exited */ }
                    }
                    return;
                }
            }

            // Also scan for any new processes matching the executable name
            await ScanForMatchingProcessesAsync(profile, targetProcessIds, cancellationToken);

            await Task.Delay(delayMs, cancellationToken);
        }

        Debug.WriteLine($"Slot {Id}: WaitForMainWindowAsync timed out after {maxAttempts * delayMs}ms");
    }

    /// <summary>
    /// Check if a window matches the launch profile criteria
    /// </summary>
    private bool IsWindowMatch(IntPtr hWnd, LaunchProfile profile)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        // If no specific criteria, accept any visible window with a title
        if (string.IsNullOrEmpty(profile.WindowClass) && string.IsNullOrEmpty(profile.WindowTitlePattern))
        {
            return WindowHelper.IsWindowVisible(hWnd) &&
                   !string.IsNullOrEmpty(WindowHelper.GetWindowTitle(hWnd));
        }

        // Check window class if specified
        if (!string.IsNullOrEmpty(profile.WindowClass))
        {
            var className = WindowHelper.GetWindowClassName(hWnd);
            if (!className.Equals(profile.WindowClass, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Check window title pattern if specified
        if (!string.IsNullOrEmpty(profile.WindowTitlePattern))
        {
            var title = WindowHelper.GetWindowTitle(hWnd);
            try
            {
                if (!Regex.IsMatch(title, profile.WindowTitlePattern, RegexOptions.IgnoreCase))
                    return false;
            }
            catch
            {
                // Invalid regex, fall back to contains
                if (!title.Contains(profile.WindowTitlePattern, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return WindowHelper.IsWindowVisible(hWnd);
    }

    /// <summary>
    /// Find a matching window from a set of process IDs.
    /// Skips windows that are already claimed by other slots.
    /// </summary>
    private IntPtr FindMatchingWindow(HashSet<int> processIds, LaunchProfile profile)
    {
        IntPtr bestMatch = IntPtr.Zero;

        var windows = WindowHelper.FindWindows(hWnd =>
        {
            // Skip windows already claimed by other slots
            if (IsWindowClaimed(hWnd))
                return false;

            Native.User32.GetWindowThreadProcessId(hWnd, out var pid);
            return processIds.Contains((int)pid) &&
                   WindowHelper.IsWindowVisible(hWnd) &&
                   !string.IsNullOrEmpty(WindowHelper.GetWindowTitle(hWnd));
        });

        foreach (var hWnd in windows)
        {
            // Double-check it's not claimed (race condition protection)
            if (IsWindowClaimed(hWnd))
                continue;

            if (IsWindowMatch(hWnd, profile))
            {
                bestMatch = hWnd;
                break;
            }
            // Keep track of any valid window as fallback
            if (bestMatch == IntPtr.Zero)
                bestMatch = hWnd;
        }

        return bestMatch;
    }

    /// <summary>
    /// Find child processes that might have been spawned by a launcher
    /// </summary>
    private async Task<Process?> FindChildProcessAsync(int parentProcessId, LaunchProfile profile, CancellationToken cancellationToken)
    {
        // Wait a moment for child process to start
        await Task.Delay(500, cancellationToken);

        // If a separate game executable is specified, look for that first
        var gameExeName = !string.IsNullOrEmpty(profile.GameExecutable)
            ? Path.GetFileNameWithoutExtension(profile.GameExecutable)
            : Path.GetFileNameWithoutExtension(profile.Executable);

        // Find processes with the game executable name
        var candidates = System.Diagnostics.Process.GetProcessesByName(gameExeName)
            .Where(p => p.Id != parentProcessId)
            .ToList();

        // If GameExecutable is different from Executable, we might need to search for the launcher's children
        if (!string.IsNullOrEmpty(profile.GameExecutable) && candidates.Count == 0)
        {
            // Also search by the launcher executable in case the game hasn't started yet
            var launcherName = Path.GetFileNameWithoutExtension(profile.Executable);
            candidates = System.Diagnostics.Process.GetProcessesByName(launcherName)
                .Where(p => p.Id != parentProcessId)
                .ToList();
        }

        // If we found candidates, return the most recent one
        if (candidates.Count > 0)
        {
            // Try to find one with a main window
            foreach (var proc in candidates)
            {
                try
                {
                    proc.Refresh();
                    if (proc.MainWindowHandle != IntPtr.Zero)
                        return proc;
                }
                catch { /* Process may have exited */ }
            }

            // Otherwise return any candidate
            return candidates.FirstOrDefault();
        }

        // Also check for common game executable patterns
        // e.g., launcher might be LaunchPad.exe but game is eqgame.exe
        if (!string.IsNullOrEmpty(profile.WindowClass))
        {
            var windows = WindowHelper.FindWindowsByClassName(profile.WindowClass);
            foreach (var hWnd in windows)
            {
                Native.User32.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid != 0 && pid != parentProcessId)
                {
                    try
                    {
                        return System.Diagnostics.Process.GetProcessById((int)pid);
                    }
                    catch { }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Scan for new processes that match the profile's executable
    /// </summary>
    private async Task ScanForMatchingProcessesAsync(LaunchProfile profile, HashSet<int> knownProcessIds, CancellationToken cancellationToken)
    {
        // Get the primary executable name to scan for
        var gameExeName = !string.IsNullOrEmpty(profile.GameExecutable)
            ? Path.GetFileNameWithoutExtension(profile.GameExecutable)
            : Path.GetFileNameWithoutExtension(profile.Executable);

        try
        {
            // Scan for game processes
            var processes = System.Diagnostics.Process.GetProcessesByName(gameExeName);
            foreach (var proc in processes)
            {
                if (!knownProcessIds.Contains(proc.Id))
                {
                    knownProcessIds.Add(proc.Id);
                }
            }

            // Also scan for launcher processes if GameExecutable is different
            if (!string.IsNullOrEmpty(profile.GameExecutable))
            {
                var launcherName = Path.GetFileNameWithoutExtension(profile.Executable);
                var launcherProcesses = System.Diagnostics.Process.GetProcessesByName(launcherName);
                foreach (var proc in launcherProcesses)
                {
                    if (!knownProcessIds.Contains(proc.Id))
                    {
                        knownProcessIds.Add(proc.Id);
                    }
                }
            }
        }
        catch { /* Ignore errors when enumerating processes */ }

        await Task.CompletedTask;
    }

    // Keep the old signature for backward compatibility
    private Task WaitForMainWindowAsync(CancellationToken cancellationToken)
    {
        return WaitForMainWindowAsync(_launchProfile ?? new LaunchProfile(), cancellationToken);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        State = SlotState.Exited;
        ProcessExited?.Invoke(this, new SlotEventArgs(this));
    }

    partial void OnStateChanged(SlotState value)
    {
        StateChanged?.Invoke(this, new SlotEventArgs(this));
    }

    /// <summary>
    /// Called when MainWindowHandle changes - manages the claim registry
    /// </summary>
    partial void OnMainWindowHandleChanging(IntPtr oldValue, IntPtr newValue)
    {
        // Release the old window
        if (oldValue != IntPtr.Zero)
        {
            ReleaseWindow(oldValue);
        }

        // Note: We don't claim here - claiming happens in WaitForMainWindowAsync
        // This is because the changing event fires BEFORE the value is set,
        // and we want to validate/claim the window before accepting it
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Release any claimed window handle
        if (MainWindowHandle != IntPtr.Zero)
        {
            ReleaseWindow(MainWindowHandle);
        }

        if (_process != null)
        {
            _process.EnableRaisingEvents = false;
            _process.Exited -= OnProcessExited;
            _process.Dispose();
            _process = null;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for slot events
/// </summary>
public class SlotEventArgs : EventArgs
{
    public Slot Slot { get; }

    public SlotEventArgs(Slot slot)
    {
        Slot = slot;
    }
}
