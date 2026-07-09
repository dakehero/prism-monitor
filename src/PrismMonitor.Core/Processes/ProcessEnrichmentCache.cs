namespace PrismMonitor.Core.Processes;

public sealed class ProcessEnrichmentCache
{
    private readonly Dictionary<int, CacheEntry> _entries = [];

    public ProcessEnrichmentInfo GetOrAdd(
        ProcessSnapshotInfo snapshot,
        Func<ProcessSnapshotInfo, ProcessEnrichmentInfo> factory)
    {
        if (_entries.TryGetValue(snapshot.ProcessId, out CacheEntry? entry)
            && entry.Matches(snapshot))
        {
            return entry.Enrichment;
        }

        ProcessEnrichmentInfo enrichment = factory(snapshot);
        _entries[snapshot.ProcessId] = new CacheEntry(snapshot.CreationTime, enrichment);
        return enrichment;
    }

    public void Prune(IEnumerable<ProcessSnapshotInfo> activeSnapshots)
    {
        HashSet<int> activeProcessIds = activeSnapshots
            .Select(snapshot => snapshot.ProcessId)
            .ToHashSet();

        foreach (int processId in _entries.Keys.Where(processId => !activeProcessIds.Contains(processId)).ToList())
        {
            _entries.Remove(processId);
        }
    }

    public bool Contains(ProcessSnapshotInfo snapshot)
    {
        return _entries.TryGetValue(snapshot.ProcessId, out CacheEntry? entry)
            && entry.Matches(snapshot);
    }

    private sealed record CacheEntry(DateTimeOffset? CreationTime, ProcessEnrichmentInfo Enrichment)
    {
        public bool Matches(ProcessSnapshotInfo snapshot)
        {
            return CreationTime is not null
                && snapshot.CreationTime is not null
                && CreationTime == snapshot.CreationTime;
        }
    }
}
