using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.History;

public sealed class LaunchHistoryRecorder
{
    private readonly HashSet<int> _seenProcessIds = [];

    public IReadOnlyList<LaunchHistoryEvent> CaptureNewEvents(
        IReadOnlyList<CompatibilityProcessInfo> processes,
        DateTimeOffset detectedAt)
    {
        List<LaunchHistoryEvent> events = [];

        foreach (CompatibilityProcessInfo process in processes)
        {
            if (!_seenProcessIds.Add(process.ProcessId))
            {
                continue;
            }

            events.Add(new LaunchHistoryEvent(
                process.Name,
                process.Architecture,
                process.ProcessId,
                process.ExecutablePath,
                StartedAt: null,
                detectedAt));
        }

        return events;
    }
}
