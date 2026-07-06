using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

Console.WriteLine("Prism Monitor process visibility probe");
Console.WriteLine($"Is elevated: {IsElevated()}");
Console.WriteLine($"OS: {Environment.OSVersion}");
Console.WriteLine($"Arch: process={RuntimeInformation.ProcessArchitecture}, os={RuntimeInformation.OSArchitecture}");
Console.WriteLine();

Dictionary<int, string> toolhelpProcesses = ProcessEnumerators.EnumerateToolhelpProcesses();
Dictionary<int, string> ntProcesses = ProcessEnumerators.EnumerateNtProcesses(out Dictionary<int, TimeSpan> ntCpuTimes);
HashSet<int> psapiProcessIds = ProcessEnumerators.EnumeratePsapiProcessIds();
Dictionary<int, string> dotnetProcesses = ProcessEnumerators.EnumerateDotNetProcesses(out HashSet<int> dotnetCpuReadable);

HashSet<int> allProcessIds = [.. toolhelpProcesses.Keys, .. ntProcesses.Keys, .. psapiProcessIds, .. dotnetProcesses.Keys];
List<AccessProbe> probes = allProcessIds
    .Order()
    .Select(pid => AccessProbe.Read(pid, PickName(pid)))
    .ToList();

Console.WriteLine("Enumeration counts");
PrintCount("Toolhelp names", toolhelpProcesses.Count);
PrintCount("NT names", ntProcesses.Count);
PrintCount("NT CPU times", ntCpuTimes.Count);
PrintCount("PSAPI PIDs", psapiProcessIds.Count);
PrintCount(".NET names", dotnetProcesses.Count);
PrintCount(".NET CPU readable", dotnetCpuReadable.Count);
PrintCount("Union PIDs", allProcessIds.Count);
Console.WriteLine();

Console.WriteLine("Handle-dependent enrichment from union PIDs");
PrintCount("OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)", probes.Count(probe => probe.Opened));
PrintCount("QueryFullProcessImageName", probes.Count(probe => probe.PathReadable));
PrintCount("IsWow64Process2", probes.Count(probe => probe.ArchReadable));
PrintCount("GetProcessTimes", probes.Count(probe => probe.ProcessTimesReadable));
PrintCount("OpenProcess denied", probes.Count(probe => probe.OpenError == 5));
Console.WriteLine();

PrintSample(
    "Visible without an openable limited-information handle",
    probes.Where(probe => !probe.Opened)
        .Take(12));

PrintSample(
    "CPU from NT snapshot, but GetProcessTimes unavailable",
    probes.Where(probe => ntCpuTimes.ContainsKey(probe.ProcessId) && !probe.ProcessTimesReadable)
        .Take(12));

PrintSample(
    "Architecture unavailable, but process still visible",
    probes.Where(probe => !probe.ArchReadable)
        .Take(12));

string PickName(int processId)
{
    if (toolhelpProcesses.TryGetValue(processId, out string? toolhelpName) && !string.IsNullOrWhiteSpace(toolhelpName))
    {
        return toolhelpName;
    }

    if (ntProcesses.TryGetValue(processId, out string? ntName) && !string.IsNullOrWhiteSpace(ntName))
    {
        return ntName;
    }

    if (dotnetProcesses.TryGetValue(processId, out string? dotnetName) && !string.IsNullOrWhiteSpace(dotnetName))
    {
        return dotnetName;
    }

    return processId == 0 ? "Idle" : "<unknown>";
}

static void PrintCount(string label, int value)
{
    Console.WriteLine($"{label,-48} {value,5}");
}

static void PrintSample(string title, IEnumerable<AccessProbe> rows)
{
    Console.WriteLine(title);
    Console.WriteLine($"{"PID",8}  {"Name",-32} {"Open",-6} {"Err",-5} {"Path",-6} {"Arch",-6} {"Times",-6}");
    foreach (AccessProbe row in rows)
    {
        string error = row.OpenError.HasValue ? row.OpenError.Value.ToString() : "";
        Console.WriteLine($"{row.ProcessId,8}  {Trim(row.Name, 32),-32} {YesNo(row.Opened),-6} {error,-5} {YesNo(row.PathReadable),-6} {YesNo(row.ArchReadable),-6} {YesNo(row.ProcessTimesReadable),-6}");
    }

    Console.WriteLine();
}

static string YesNo(bool value)
{
    return value ? "yes" : "no";
}

static string Trim(string value, int maxLength)
{
    return value.Length <= maxLength ? value : value[..(maxLength - 1)] + ".";
}

static bool IsElevated()
{
    using System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    System.Security.Principal.WindowsPrincipal principal = new(identity);
    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}

internal sealed record AccessProbe(
    int ProcessId,
    string Name,
    bool Opened,
    int? OpenError,
    bool PathReadable,
    bool ArchReadable,
    bool ProcessTimesReadable)
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public static AccessProbe Read(int processId, string name)
    {
        if (processId == 0)
        {
            return new AccessProbe(processId, name, false, null, false, false, false);
        }

        nint handle = Native.OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == 0)
        {
            return new AccessProbe(processId, name, false, Marshal.GetLastPInvokeError(), false, false, false);
        }

        try
        {
            bool pathReadable = TryReadPath(handle);
            bool archReadable = Native.IsWow64Process2(handle, out _, out _);
            bool processTimesReadable = Native.GetProcessTimes(handle, out _, out _, out _, out _);
            return new AccessProbe(processId, name, true, null, pathReadable, archReadable, processTimesReadable);
        }
        finally
        {
            _ = Native.CloseHandle(handle);
        }
    }

    private static bool TryReadPath(nint handle)
    {
        char[] buffer = new char[32_767];
        uint length = (uint)buffer.Length;
        return Native.QueryFullProcessImageName(handle, 0, buffer, ref length) && length > 0;
    }
}

internal static class ProcessEnumerators
{
    public static Dictionary<int, string> EnumerateDotNetProcesses(out HashSet<int> cpuReadable)
    {
        Dictionary<int, string> processes = [];
        cpuReadable = [];

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    processes[process.Id] = process.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                catch (Win32Exception)
                {
                    continue;
                }

                try
                {
                    _ = process.TotalProcessorTime;
                    cpuReadable.Add(process.Id);
                }
                catch (InvalidOperationException)
                {
                }
                catch (Win32Exception)
                {
                }
            }
        }

        return processes;
    }

    public static HashSet<int> EnumeratePsapiProcessIds()
    {
        uint[] processIds = new uint[1024];
        while (true)
        {
            uint bufferBytes = checked((uint)(processIds.Length * sizeof(uint)));
            if (!Native.EnumProcesses(processIds, bufferBytes, out uint bytesReturned))
            {
                return [];
            }

            int count = checked((int)(bytesReturned / sizeof(uint)));
            if (count < processIds.Length)
            {
                return processIds.Take(count).Select(id => checked((int)id)).ToHashSet();
            }

            processIds = new uint[processIds.Length * 2];
        }
    }

    public static Dictionary<int, string> EnumerateToolhelpProcesses()
    {
        Dictionary<int, string> processes = [];
        nint snapshot = Native.CreateToolhelp32Snapshot(Native.Th32csSnapprocess, 0);
        if (snapshot == Native.InvalidHandleValue)
        {
            return processes;
        }

        try
        {
            ProcessEntry32 entry = new()
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Native.Process32First(snapshot, ref entry))
            {
                return processes;
            }

            do
            {
                processes[checked((int)entry.ProcessId)] = entry.ExeFile;
            }
            while (Native.Process32Next(snapshot, ref entry));

            return processes;
        }
        finally
        {
            _ = Native.CloseHandle(snapshot);
        }
    }

    public static Dictionary<int, string> EnumerateNtProcesses(out Dictionary<int, TimeSpan> cpuTimes)
    {
        Dictionary<int, string> processes = [];
        cpuTimes = [];
        int bufferLength = 1 << 20;

        while (true)
        {
            nint buffer = Marshal.AllocHGlobal(bufferLength);
            try
            {
                int status = Native.NtQuerySystemInformation(
                    Native.SystemProcessInformation,
                    buffer,
                    bufferLength,
                    out int returnLength);

                if (status == Native.StatusInfoLengthMismatch)
                {
                    bufferLength = Math.Max(bufferLength * 2, returnLength + 64 * 1024);
                    continue;
                }

                if (status < 0)
                {
                    return processes;
                }

                nint current = buffer;
                while (true)
                {
                    SystemProcessInformation info = Marshal.PtrToStructure<SystemProcessInformation>(current);
                    int processId = checked((int)info.UniqueProcessId);
                    string name = ReadUnicodeString(info.ImageName);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = processId == 0 ? "Idle" : "System";
                    }

                    processes[processId] = name;
                    cpuTimes[processId] = TimeSpan.FromTicks(info.KernelTime + info.UserTime);

                    if (info.NextEntryOffset == 0)
                    {
                        break;
                    }

                    current += checked((int)info.NextEntryOffset);
                }

                return processes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static string ReadUnicodeString(UnicodeString value)
    {
        if (value.Length == 0 || value.Buffer == 0)
        {
            return string.Empty;
        }

        return Marshal.PtrToStringUni(value.Buffer, value.Length / 2) ?? string.Empty;
    }
}

internal static partial class Native
{
    public const uint Th32csSnapprocess = 0x00000002;
    public const int SystemProcessInformation = 5;
    public const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    public static readonly nint InvalidHandleValue = -1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWow64Process2(nint process, out ushort processMachine, out ushort nativeMachine);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetProcessTimes(nint process, out long creationTime, out long exitTime, out long kernelTime, out long userTime);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint process, uint flags, char[] executableName, ref uint size);

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumProcesses([Out] uint[] processIds, uint bytes, out uint bytesReturned);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32First(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32Next(nint snapshot, ref ProcessEntry32 entry);

    [LibraryImport("ntdll.dll")]
    public static partial int NtQuerySystemInformation(int systemInformationClass, nint systemInformation, int systemInformationLength, out int returnLength);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ProcessEntry32
{
    public uint Size;
    public uint Usage;
    public uint ProcessId;
    public nint DefaultHeapId;
    public uint ModuleId;
    public uint Threads;
    public uint ParentProcessId;
    public int PriorityClassBase;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string ExeFile;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UnicodeString
{
    public readonly ushort Length;
    public readonly ushort MaximumLength;
    public readonly nint Buffer;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct SystemProcessInformation
{
    public readonly uint NextEntryOffset;
    public readonly uint NumberOfThreads;
    public readonly long WorkingSetPrivateSize;
    public readonly uint HardFaultCount;
    public readonly uint NumberOfThreadsHighWatermark;
    public readonly ulong CycleTime;
    public readonly long CreateTime;
    public readonly long UserTime;
    public readonly long KernelTime;
    public readonly UnicodeString ImageName;
    public readonly int BasePriority;
    public readonly nint UniqueProcessId;
    public readonly nint InheritedFromUniqueProcessId;
}
