namespace PrismMonitor.Core.Processes;

public sealed class ProcessSnapshotTracker
{
    private readonly Dictionary<int, TrackedProcess> _tracked = [];

    public IReadOnlyList<ProcessSnapshotInfo> Track(
        IReadOnlyList<ProcessObservation> observations,
        DateTimeOffset detectedAt)
    {
        HashSet<int> activeIds = observations
            .Select(observation => observation.ProcessId)
            .ToHashSet();

        foreach (int exitedId in _tracked.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            _tracked.Remove(exitedId);
        }

        List<ProcessSnapshotInfo> snapshots = [];
        foreach (ProcessObservation observation in observations.DistinctBy(item => item.ProcessId))
        {
            if (!_tracked.TryGetValue(observation.ProcessId, out TrackedProcess? tracked)
                || !tracked.Matches(observation))
            {
                tracked = new TrackedProcess(
                    observation.Name,
                    observation.CreationTime,
                    detectedAt,
                    new ProcessInstanceKey(
                        observation.ProcessId,
                        observation.CreationTime ?? detectedAt,
                        observation.CreationTime is not null));
                _tracked[observation.ProcessId] = tracked;
            }

            snapshots.Add(new ProcessSnapshotInfo(
                observation.ProcessId,
                observation.Name,
                observation.CpuTime,
                observation.CreationTime,
                tracked.DetectedAt,
                tracked.Key));
        }

        return snapshots;
    }

    private sealed record TrackedProcess(
        string Name,
        DateTimeOffset? CreationTime,
        DateTimeOffset DetectedAt,
        ProcessInstanceKey Key)
    {
        public bool Matches(ProcessObservation observation)
        {
            return CreationTime is not null || observation.CreationTime is not null
                ? CreationTime == observation.CreationTime
                : string.Equals(Name, observation.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
