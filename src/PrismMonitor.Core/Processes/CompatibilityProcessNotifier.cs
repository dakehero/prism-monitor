namespace PrismMonitor.Core.Processes;

public sealed class CompatibilityProcessNotifier
{
    private HashSet<ProcessInstanceKey> _knownInstances = [];
    private bool _hasBaseline;

    public IReadOnlyList<CompatibilityProcessInfo> CaptureNewProcesses(IReadOnlyList<CompatibilityProcessInfo> currentProcesses)
    {
        HashSet<ProcessInstanceKey> currentInstances = currentProcesses
            .Select(GetInstanceKey)
            .ToHashSet();

        if (!_hasBaseline)
        {
            _knownInstances = currentInstances;
            _hasBaseline = true;
            return [];
        }

        List<CompatibilityProcessInfo> newProcesses = currentProcesses
            .Where(process => !_knownInstances.Contains(GetInstanceKey(process)))
            .ToList();

        _knownInstances = currentInstances;
        return newProcesses;
    }

    private static ProcessInstanceKey GetInstanceKey(CompatibilityProcessInfo process)
    {
        return process.InstanceKey ?? new ProcessInstanceKey(
            process.ProcessId,
            process.DetectedAt ?? process.CreationTime ?? DateTimeOffset.MinValue,
            process.CreationTime is not null);
    }
}
