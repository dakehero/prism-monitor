using System.Diagnostics;
using NativeGuard.Core.Processes;
using NativeGuard_App.Interop;

namespace NativeGuard_App.Processes;

internal sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    public Task<IReadOnlyList<NonNativeProcessInfo>> GetNonNativeProcessesAsync(CancellationToken cancellationToken = default)
    {
        List<NonNativeProcessInfo> processes = [];

        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NonNativeProcessInfo? processInfo = TryReadProcess(process);
                if (processInfo is not null)
                {
                    processes.Add(processInfo);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<NonNativeProcessInfo>>(processes);
    }

    private static NonNativeProcessInfo? TryReadProcess(Process process)
    {
        try
        {
            if (!NativeMethods.IsWow64Process2(process.SafeHandle, out ushort processMachine, out ushort nativeMachine))
            {
                return null;
            }

            ProcessArchitectureInfo architecture = ProcessArchitectureClassifier.Classify(processMachine, nativeMachine);
            if (!architecture.IsNonNative)
            {
                return null;
            }

            return new NonNativeProcessInfo(
                process.ProcessName,
                process.Id,
                architecture.DisplayName,
                process.TotalProcessorTime);
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
}
