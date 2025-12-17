using System.Runtime.InteropServices;

namespace Multiboxer.Native;

/// <summary>
/// P/Invoke declarations for kernel32.dll - Process and system APIs
/// </summary>
public static class Kernel32
{
    #region Process Management

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll")]
    public static extern int GetProcessId(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    public static extern int GetCurrentProcessId();

    #endregion

    #region CPU Affinity

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);

    [DllImport("kernel32.dll")]
    public static extern int GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

    #endregion

    #region Process Priority

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetPriorityClass(IntPtr hProcess, ProcessPriorityClass dwPriorityClass);

    [DllImport("kernel32.dll")]
    public static extern ProcessPriorityClass GetPriorityClass(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetThreadPriority(IntPtr hThread, ThreadPriority nPriority);

    [DllImport("kernel32.dll")]
    public static extern ThreadPriority GetThreadPriority(IntPtr hThread);

    #endregion

    #region Module Information

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    #endregion

    #region System Information

    [DllImport("kernel32.dll")]
    public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll")]
    public static extern int GetLastError();

    #endregion

    #region Job Objects (for process grouping)

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoClass jobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool QueryInformationJobObject(IntPtr hJob, JobObjectInfoClass jobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength, out uint lpReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

    #endregion
}

#region Enums and Structs

[Flags]
public enum ProcessAccessFlags : uint
{
    All = 0x001F0FFF,
    Terminate = 0x00000001,
    CreateThread = 0x00000002,
    VirtualMemoryOperation = 0x00000008,
    VirtualMemoryRead = 0x00000010,
    VirtualMemoryWrite = 0x00000020,
    DuplicateHandle = 0x00000040,
    CreateProcess = 0x000000080,
    SetQuota = 0x00000100,
    SetInformation = 0x00000200,
    QueryInformation = 0x00000400,
    QueryLimitedInformation = 0x00001000,
    Synchronize = 0x00100000
}

public enum ProcessPriorityClass : uint
{
    Idle = 0x40,
    BelowNormal = 0x4000,
    Normal = 0x20,
    AboveNormal = 0x8000,
    High = 0x80,
    RealTime = 0x100
}

public enum ThreadPriority : int
{
    Idle = -15,
    Lowest = -2,
    BelowNormal = -1,
    Normal = 0,
    AboveNormal = 1,
    Highest = 2,
    TimeCritical = 15
}

public enum JobObjectInfoClass : int
{
    BasicAccountingInformation = 1,
    BasicLimitInformation = 2,
    BasicProcessIdList = 3,
    BasicUIRestrictions = 4,
    SecurityLimitInformation = 5,
    EndOfJobTimeInformation = 6,
    AssociateCompletionPortInformation = 7,
    BasicAndIoAccountingInformation = 8,
    ExtendedLimitInformation = 9,
    JobSetInformation = 10,
    GroupInformation = 11,
    NotificationLimitInformation = 12,
    LimitViolationInformation = 13,
    GroupInformationEx = 14,
    CpuRateControlInformation = 15,
    NetRateControlInformation = 32
}

[StructLayout(LayoutKind.Sequential)]
public struct SYSTEM_INFO
{
    public ushort processorArchitecture;
    public ushort reserved;
    public uint pageSize;
    public IntPtr minimumApplicationAddress;
    public IntPtr maximumApplicationAddress;
    public IntPtr activeProcessorMask;
    public uint numberOfProcessors;
    public uint processorType;
    public uint allocationGranularity;
    public ushort processorLevel;
    public ushort processorRevision;
}

[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
{
    public long PerProcessUserTimeLimit;
    public long PerJobUserTimeLimit;
    public uint LimitFlags;
    public UIntPtr MinimumWorkingSetSize;
    public UIntPtr MaximumWorkingSetSize;
    public uint ActiveProcessLimit;
    public IntPtr Affinity;
    public uint PriorityClass;
    public uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
public struct IO_COUNTERS
{
    public ulong ReadOperationCount;
    public ulong WriteOperationCount;
    public ulong OtherOperationCount;
    public ulong ReadTransferCount;
    public ulong WriteTransferCount;
    public ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
{
    public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
    public IO_COUNTERS IoInfo;
    public UIntPtr ProcessMemoryLimit;
    public UIntPtr JobMemoryLimit;
    public UIntPtr PeakProcessMemoryUsed;
    public UIntPtr PeakJobMemoryUsed;
}

[Flags]
public enum JobObjectLimitFlags : uint
{
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000,
    JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800,
    JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000,
    JOB_OBJECT_LIMIT_AFFINITY = 0x00000010,
    JOB_OBJECT_LIMIT_PRIORITY_CLASS = 0x00000020,
    JOB_OBJECT_LIMIT_SCHEDULING_CLASS = 0x00000080,
    JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100,
    JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200,
    JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008,
    JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001,
    JOB_OBJECT_LIMIT_PROCESS_TIME = 0x00000002,
    JOB_OBJECT_LIMIT_JOB_TIME = 0x00000004
}

#endregion
