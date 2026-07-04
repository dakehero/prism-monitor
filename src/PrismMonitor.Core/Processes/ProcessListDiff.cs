namespace PrismMonitor.Core.Processes;

public sealed record ProcessListDiff(
    IReadOnlyList<CompatibilityProcessInfo> Added,
    IReadOnlyList<CompatibilityProcessInfo> Updated,
    IReadOnlyList<int> RemovedProcessIds,
    IReadOnlyList<CompatibilityProcessInfo> SortedRows);
