using NativeGuard.Core.Processes;
using NativeGuard_App.Interop;

namespace NativeGuard_App.Processes;

internal sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    private const int InitialProcessCapacity = 1024;
    private const int MaxPath = 32_767;

    public Task<IReadOnlyList<NonNativeProcessInfo>> GetNonNativeProcessesAsync(CancellationToken cancellationToken = default)
    {
        List<NonNativeProcessInfo> processes = [];

        foreach (uint processId in EnumerateProcessIds())
        {
            cancellationToken.ThrowIfCancellationRequested();

            NonNativeProcessInfo? process = TryReadProcess(processId);
            if (process is not null)
            {
                processes.Add(process);
            }
        }

        return Task.FromResult<IReadOnlyList<NonNativeProcessInfo>>(processes);
    }

    private static IEnumerable<uint> EnumerateProcessIds()
    {
        uint[] processIds = new uint[InitialProcessCapacity];

        while (true)
        {
            uint bufferBytes = checked((uint)(processIds.Length * sizeof(uint)));
            if (!NativeMethods.EnumProcesses(processIds, bufferBytes, out uint bytesReturned))
            {
                yield break;
            }

            int processCount = checked((int)(bytesReturned / sizeof(uint)));
            if (processCount < processIds.Length)
            {
                for (int index = 0; index < processCount; index++)
                {
                    yield return processIds[index];
                }

                yield break;
            }

            processIds = new uint[processIds.Length * 2];
        }
    }

    private static NonNativeProcessInfo? TryReadProcess(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        IntPtr processHandle = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation,
            inheritHandle: false,
            processId);

        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (!NativeMethods.IsWow64Process2(processHandle, out ushort processMachineValue, out ushort nativeMachineValue))
            {
                return null;
            }

            ProcessArchitectureInfo architecture = ProcessArchitectureClassifier.Classify(
                (MachineType)processMachineValue,
                (MachineType)nativeMachineValue);

            if (!architecture.IsNonNative)
            {
                return null;
            }

            if (!NativeMethods.GetProcessTimes(
                    processHandle,
                    out _,
                    out _,
                    out FileTime kernelTime,
                    out FileTime userTime))
            {
                return null;
            }

            string? processName = TryReadProcessName(processHandle);
            if (string.IsNullOrWhiteSpace(processName))
            {
                return null;
            }

            TimeSpan cpuTime = TimeSpan.FromTicks(kernelTime.ToTicks() + userTime.ToTicks());
            return new NonNativeProcessInfo(processName, checked((int)processId), architecture.DisplayName, cpuTime);
        }
        finally
        {
            _ = NativeMethods.CloseHandle(processHandle);
        }
    }

    private static string? TryReadProcessName(IntPtr processHandle)
    {
        Span<char> buffer = stackalloc char[MaxPath];
        uint length = (uint)buffer.Length;

        if (!NativeMethods.QueryFullProcessImageName(processHandle, 0, buffer, ref length) || length == 0)
        {
            return null;
        }

        string path = new(buffer[..checked((int)length)]);
        string fileName = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }
}
