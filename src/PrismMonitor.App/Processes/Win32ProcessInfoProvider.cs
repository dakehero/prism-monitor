using System.Diagnostics;
using System.Reflection.PortableExecutable;
using PrismMonitor.Core.Processes;
using PrismMonitor.App.Interop;

namespace PrismMonitor.App.Processes;

internal sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    private const int MaxPath = 32_767;

    public Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default)
    {
        List<CompatibilityProcessInfo> processes = [];

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CompatibilityProcessInfo? processInfo = TryReadProcess(process);
                if (processInfo is not null)
                {
                    processes.Add(processInfo);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<CompatibilityProcessInfo>>(processes);
    }

    private static CompatibilityProcessInfo? TryReadProcess(Process process)
    {
        try
        {
            if (!ProcessInterop.IsWow64Process2(process.SafeHandle, out ushort processMachine, out ushort nativeMachine))
            {
                return null;
            }

            string? executablePath = TryReadProcessImagePath(process);
            ushort? imageMachine = processMachine == 0
                ? TryReadImageMachine(executablePath)
                : null;
            ProcessArchitectureInfo architecture = ProcessArchitectureClassifier.Classify(processMachine, nativeMachine, imageMachine);
            if (!architecture.IsCompatibility)
            {
                return null;
            }

            return new CompatibilityProcessInfo(
                process.ProcessName,
                process.Id,
                architecture.DisplayName,
                process.TotalProcessorTime,
                executablePath);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static ushort? TryReadImageMachine(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader reader = new(stream);
            return (ushort)reader.PEHeaders.CoffHeader.Machine;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? TryReadProcessImagePath(Process process)
    {
        char[] buffer = new char[MaxPath];
        uint length = (uint)buffer.Length;

        if (!ProcessInterop.QueryFullProcessImageName(process.SafeHandle, 0, buffer, ref length) || length == 0)
        {
            return null;
        }

        return new string(buffer, 0, checked((int)length));
    }
}
