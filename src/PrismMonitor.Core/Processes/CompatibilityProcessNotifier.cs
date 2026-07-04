namespace PrismMonitor.Core.Processes;

public sealed class CompatibilityProcessNotifier
{
    private HashSet<int> _knownProcessIds = [];
    private bool _hasBaseline;

    public IReadOnlyList<CompatibilityProcessInfo> CaptureNewProcesses(IReadOnlyList<CompatibilityProcessInfo> currentProcesses)
    {
        HashSet<int> currentProcessIds = currentProcesses
            .Select(process => process.ProcessId)
            .ToHashSet();

        if (!_hasBaseline)
        {
            _knownProcessIds = currentProcessIds;
            _hasBaseline = true;
            return [];
        }

        List<CompatibilityProcessInfo> newProcesses = currentProcesses
            .Where(process => !_knownProcessIds.Contains(process.ProcessId))
            .ToList();

        _knownProcessIds = currentProcessIds;
        return newProcesses;
    }
}
