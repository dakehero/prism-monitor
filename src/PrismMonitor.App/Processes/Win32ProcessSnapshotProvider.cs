using System.Diagnostics;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;

namespace PrismMonitor.App.Processes;

internal sealed class Win32ProcessSnapshotProvider : IProcessSnapshotProvider
{
    public Task<IReadOnlyList<ProcessObservation>> CaptureAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Capture(cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<ProcessObservation> Capture(CancellationToken cancellationToken)
    {
        List<ProcessObservation> observations = [];
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadIdentity(process, out int processId, out string processName))
                {
                    continue;
                }

                observations.Add(new ProcessObservation(
                    processId,
                    processName,
                    TryReadCpuTime(process),
                    TryReadStartTime(process)));
            }
        }

        return observations;
    }

    private static bool TryReadIdentity(Process process, out int processId, out string processName)
    {
        try
        {
            processId = process.Id;
            processName = process.ProcessName;
            return true;
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (NotSupportedException)
        {
        }

        processId = 0;
        processName = string.Empty;
        return false;
    }

    private static TimeSpan? TryReadCpuTime(Process process)
    {
        try
        {
            return process.TotalProcessorTime;
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

    private static DateTimeOffset? TryReadStartTime(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
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
