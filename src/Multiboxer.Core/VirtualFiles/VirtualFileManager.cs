using System.Security.Principal;
using Multiboxer.Core.Config;

namespace Multiboxer.Core.VirtualFiles;

/// <summary>
/// Manages virtual file redirection using symlinks for per-slot configuration files
/// </summary>
public class VirtualFileManager
{
    private readonly string _backupDirectory;
    private readonly Dictionary<string, SymlinkInfo> _activeSymlinks = new();
    private readonly object _lock = new();

    /// <summary>
    /// Whether the current process has admin privileges (required for symlinks)
    /// </summary>
    public static bool HasAdminPrivileges
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public VirtualFileManager(string backupDirectory)
    {
        _backupDirectory = backupDirectory;
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Setup virtual file redirections for a slot based on profile settings
    /// </summary>
    public bool SetupVirtualFiles(int slotId, LaunchProfile profile)
    {
        if (!profile.UseVirtualFiles || profile.VirtualFiles.Count == 0)
            return true;

        if (!HasAdminPrivileges)
        {
            // Symlinks require admin on Windows (unless Developer Mode is enabled)
            return false;
        }

        lock (_lock)
        {
            foreach (var mapping in profile.VirtualFiles)
            {
                var originalPath = ResolveOriginalPath(mapping.Pattern, profile.Path);
                var redirectPath = mapping.GetReplacementForSlot(slotId, profile.Path);

                if (!SetupSymlink(slotId, originalPath, redirectPath))
                {
                    // Cleanup on failure
                    CleanupSlotSymlinks(slotId);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Setup a single symlink redirection
    /// </summary>
    private bool SetupSymlink(int slotId, string originalPath, string redirectPath)
    {
        try
        {
            // Ensure redirect file exists (copy from original if needed)
            if (!File.Exists(redirectPath) && File.Exists(originalPath))
            {
                var redirectDir = Path.GetDirectoryName(redirectPath);
                if (!string.IsNullOrEmpty(redirectDir))
                {
                    Directory.CreateDirectory(redirectDir);
                }
                File.Copy(originalPath, redirectPath);
            }

            // Backup original if it's not already a symlink
            if (File.Exists(originalPath) && !IsSymlink(originalPath))
            {
                var backupPath = GetBackupPath(originalPath);
                var backupDir = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Move original to backup
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(originalPath, backupPath);
            }
            else if (File.Exists(originalPath) && IsSymlink(originalPath))
            {
                // Remove existing symlink
                File.Delete(originalPath);
            }

            // Create symlink: original -> redirect
            CreateSymlink(originalPath, redirectPath);

            // Track this symlink
            var key = $"{slotId}:{originalPath}";
            _activeSymlinks[key] = new SymlinkInfo
            {
                SlotId = slotId,
                OriginalPath = originalPath,
                RedirectPath = redirectPath,
                BackupPath = GetBackupPath(originalPath)
            };

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Remove virtual file redirections for a slot
    /// </summary>
    public void CleanupSlotSymlinks(int slotId)
    {
        lock (_lock)
        {
            var keysToRemove = _activeSymlinks
                .Where(kvp => kvp.Value.SlotId == slotId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                var info = _activeSymlinks[key];
                RestoreOriginal(info);
                _activeSymlinks.Remove(key);
            }
        }
    }

    /// <summary>
    /// Remove all virtual file redirections
    /// </summary>
    public void CleanupAll()
    {
        lock (_lock)
        {
            foreach (var info in _activeSymlinks.Values)
            {
                RestoreOriginal(info);
            }
            _activeSymlinks.Clear();
        }
    }

    /// <summary>
    /// Restore the original file from backup
    /// </summary>
    private void RestoreOriginal(SymlinkInfo info)
    {
        try
        {
            // Remove symlink
            if (File.Exists(info.OriginalPath) && IsSymlink(info.OriginalPath))
            {
                File.Delete(info.OriginalPath);
            }

            // Restore backup
            if (File.Exists(info.BackupPath))
            {
                File.Move(info.BackupPath, info.OriginalPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Resolve pattern to full path
    /// </summary>
    private string ResolveOriginalPath(string pattern, string gamePath)
    {
        // Handle patterns like "eqclient.ini" or "*/eqclient.ini"
        if (pattern.StartsWith("*/"))
        {
            pattern = pattern.Substring(2);
        }

        if (Path.IsPathRooted(pattern))
        {
            return pattern;
        }

        return Path.Combine(gamePath, pattern);
    }

    /// <summary>
    /// Get backup path for an original file
    /// </summary>
    private string GetBackupPath(string originalPath)
    {
        var fileName = Path.GetFileName(originalPath);
        var hash = originalPath.GetHashCode().ToString("X8");
        return Path.Combine(_backupDirectory, $"{hash}_{fileName}");
    }

    /// <summary>
    /// Check if a path is a symbolic link
    /// </summary>
    private static bool IsSymlink(string path)
    {
        var fileInfo = new FileInfo(path);
        return fileInfo.Exists && fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    /// <summary>
    /// Create a symbolic link
    /// </summary>
    private static void CreateSymlink(string linkPath, string targetPath)
    {
        // Use File.CreateSymbolicLink (.NET 6+)
        File.CreateSymbolicLink(linkPath, targetPath);
    }

    /// <summary>
    /// Create per-slot configuration files from a template
    /// </summary>
    public static void CreateSlotConfigFiles(LaunchProfile profile, int maxSlots = 40)
    {
        foreach (var mapping in profile.VirtualFiles)
        {
            var originalPath = Path.Combine(profile.Path, mapping.Pattern.TrimStart('*', '/'));

            if (!File.Exists(originalPath))
                continue;

            for (int slot = 1; slot <= maxSlots; slot++)
            {
                var slotPath = mapping.GetReplacementForSlot(slot, profile.Path);

                if (!File.Exists(slotPath))
                {
                    var slotDir = Path.GetDirectoryName(slotPath);
                    if (!string.IsNullOrEmpty(slotDir))
                    {
                        Directory.CreateDirectory(slotDir);
                    }
                    File.Copy(originalPath, slotPath);
                }
            }
        }
    }
}

/// <summary>
/// Information about an active symlink
/// </summary>
internal class SymlinkInfo
{
    public int SlotId { get; init; }
    public string OriginalPath { get; init; } = string.Empty;
    public string RedirectPath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
}
