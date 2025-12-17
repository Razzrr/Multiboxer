using System.Diagnostics;
using Multiboxer.Native;

namespace Multiboxer.Core.Performance;

/// <summary>
/// Manages CPU affinity for processes
/// </summary>
public class AffinityManager
{
    private readonly Dictionary<int, IntPtr> _originalAffinities = new();

    /// <summary>
    /// Number of logical processors available
    /// </summary>
    public int ProcessorCount { get; }

    /// <summary>
    /// System affinity mask (all available processors)
    /// </summary>
    public IntPtr SystemAffinityMask { get; }

    public AffinityManager()
    {
        ProcessorCount = Environment.ProcessorCount;

        // Get system affinity mask
        var currentProcess = Kernel32.GetCurrentProcess();
        Kernel32.GetProcessAffinityMask(currentProcess, out _, out var systemMask);
        SystemAffinityMask = systemMask;
    }

    /// <summary>
    /// Set process affinity to specific cores
    /// </summary>
    /// <param name="process">Target process</param>
    /// <param name="coreIndices">Array of core indices (0-based)</param>
    /// <returns>True if successful</returns>
    public bool SetProcessAffinity(Process process, params int[] coreIndices)
    {
        if (process == null || process.HasExited)
            return false;

        try
        {
            // Save original affinity if not already saved
            if (!_originalAffinities.ContainsKey(process.Id))
            {
                _originalAffinities[process.Id] = process.ProcessorAffinity;
            }

            // Calculate affinity mask
            long mask = 0;
            foreach (var core in coreIndices)
            {
                if (core >= 0 && core < ProcessorCount)
                {
                    mask |= (1L << core);
                }
            }

            if (mask == 0)
                return false;

            process.ProcessorAffinity = new IntPtr(mask);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set affinity: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set process affinity using a bitmask
    /// </summary>
    public bool SetProcessAffinityMask(Process process, long affinityMask)
    {
        if (process == null || process.HasExited)
            return false;

        try
        {
            if (!_originalAffinities.ContainsKey(process.Id))
            {
                _originalAffinities[process.Id] = process.ProcessorAffinity;
            }

            process.ProcessorAffinity = new IntPtr(affinityMask);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set affinity mask: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pin a process to a single core
    /// </summary>
    public bool PinToCore(Process process, int coreIndex)
    {
        return SetProcessAffinity(process, coreIndex);
    }

    /// <summary>
    /// Distribute multiple processes across available cores
    /// </summary>
    /// <param name="processes">Processes to distribute</param>
    /// <param name="startCore">Starting core index</param>
    /// <param name="coresPerProcess">Number of cores per process (default 1)</param>
    public void DistributeAcrossCores(IEnumerable<Process> processes, int startCore = 0, int coresPerProcess = 1)
    {
        var processList = processes.Where(p => p != null && !p.HasExited).ToList();
        if (processList.Count == 0)
            return;

        int currentCore = startCore;

        foreach (var process in processList)
        {
            var cores = new int[coresPerProcess];
            for (int i = 0; i < coresPerProcess; i++)
            {
                cores[i] = (currentCore + i) % ProcessorCount;
            }

            SetProcessAffinity(process, cores);
            currentCore = (currentCore + coresPerProcess) % ProcessorCount;
        }
    }

    /// <summary>
    /// Restore original affinity for a process
    /// </summary>
    public bool RestoreOriginalAffinity(Process process)
    {
        if (process == null || process.HasExited)
            return false;

        if (_originalAffinities.TryGetValue(process.Id, out var originalMask))
        {
            try
            {
                process.ProcessorAffinity = originalMask;
                _originalAffinities.Remove(process.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Restore original affinity for all tracked processes
    /// </summary>
    public void RestoreAllOriginalAffinities()
    {
        foreach (var kvp in _originalAffinities.ToList())
        {
            try
            {
                var process = Process.GetProcessById(kvp.Key);
                if (process != null && !process.HasExited)
                {
                    process.ProcessorAffinity = kvp.Value;
                }
            }
            catch
            {
                // Process no longer exists
            }
        }

        _originalAffinities.Clear();
    }

    /// <summary>
    /// Get current affinity mask for a process
    /// </summary>
    public long GetProcessAffinity(Process process)
    {
        if (process == null || process.HasExited)
            return 0;

        try
        {
            return process.ProcessorAffinity.ToInt64();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get list of cores a process is assigned to
    /// </summary>
    public int[] GetProcessCores(Process process)
    {
        var mask = GetProcessAffinity(process);
        var cores = new List<int>();

        for (int i = 0; i < ProcessorCount; i++)
        {
            if ((mask & (1L << i)) != 0)
            {
                cores.Add(i);
            }
        }

        return cores.ToArray();
    }

    /// <summary>
    /// Create an affinity mask from core indices
    /// </summary>
    public static long CreateAffinityMask(params int[] coreIndices)
    {
        long mask = 0;
        foreach (var core in coreIndices)
        {
            if (core >= 0 && core < 64)
            {
                mask |= (1L << core);
            }
        }
        return mask;
    }

    /// <summary>
    /// Get core indices from an affinity mask
    /// </summary>
    public static int[] GetCoresFromMask(long mask)
    {
        var cores = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            if ((mask & (1L << i)) != 0)
            {
                cores.Add(i);
            }
        }
        return cores.ToArray();
    }
}
