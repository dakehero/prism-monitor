namespace NativeGuard.Core.Processes;

public sealed class NonNativeProcessNotifier
{
    private HashSet<int> _knownProcessIds = [];
    private bool _hasBaseline;

    public IReadOnlyList<NonNativeProcessInfo> CaptureNewProcesses(IReadOnlyList<NonNativeProcessInfo> currentProcesses)
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

        List<NonNativeProcessInfo> newProcesses = currentProcesses
            .Where(process => !_knownProcessIds.Contains(process.ProcessId))
            .ToList();

        _knownProcessIds = currentProcessIds;
        return newProcesses;
    }
}
