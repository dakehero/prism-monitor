using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.History;

public sealed class LaunchHistoryRecorder
{
    private readonly HashSet<ProcessInstanceKey> _seenInstances = [];

    public IReadOnlyList<LaunchHistoryEvent> CaptureNewEvents(
        IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        Dictionary<ProcessInstanceKey, CompatibilityProcessInfo> processesByInstance = processes
            .ToDictionary(GetInstanceKey);
        _seenInstances.IntersectWith(processesByInstance.Keys);
        List<LaunchHistoryEvent> events = [];

        foreach ((ProcessInstanceKey instanceKey, CompatibilityProcessInfo process) in processesByInstance)
        {
            if (!_seenInstances.Add(instanceKey))
            {
                continue;
            }

            events.Add(new LaunchHistoryEvent(
                process.Name,
                process.Architecture,
                process.ProcessId,
                process.ExecutablePath,
                process.CreationTime,
                process.DetectedAt ?? process.CreationTime ?? DateTimeOffset.MinValue,
                process.PackageIdentity,
                process.PublisherIdentity,
                instanceKey));
        }

        return events;
    }

    private static ProcessInstanceKey GetInstanceKey(CompatibilityProcessInfo process)
    {
        return process.InstanceKey ?? new ProcessInstanceKey(
            process.ProcessId,
            process.DetectedAt ?? process.CreationTime ?? DateTimeOffset.MinValue,
            process.CreationTime is not null);
    }
}
