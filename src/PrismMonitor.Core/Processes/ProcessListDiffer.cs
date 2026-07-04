namespace PrismMonitor.Core.Processes;

public static class ProcessListDiffer
{
    public static ProcessListDiff Diff(IEnumerable<int> existingProcessIds, IReadOnlyList<CompatibilityProcessInfo> snapshot)
    {
        HashSet<int> existingIds = existingProcessIds.ToHashSet();
        HashSet<int> snapshotIds = snapshot.Select(process => process.ProcessId).ToHashSet();
        List<CompatibilityProcessInfo> added = [];
        List<CompatibilityProcessInfo> updated = [];

        foreach (CompatibilityProcessInfo process in snapshot)
        {
            if (existingIds.Contains(process.ProcessId))
            {
                updated.Add(process);
            }
            else
            {
                added.Add(process);
            }
        }

        List<int> removedProcessIds = existingIds
            .Where(processId => !snapshotIds.Contains(processId))
            .Order()
            .ToList();

        List<CompatibilityProcessInfo> sortedRows = snapshot
            .OrderByDescending(process => process.CpuTime)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.ProcessId)
            .ToList();

        return new ProcessListDiff(added, updated, removedProcessIds, sortedRows);
    }
}
