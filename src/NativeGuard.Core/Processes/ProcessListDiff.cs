namespace NativeGuard.Core.Processes;

public sealed record ProcessListDiff(
    IReadOnlyList<NonNativeProcessInfo> Added,
    IReadOnlyList<NonNativeProcessInfo> Updated,
    IReadOnlyList<int> RemovedProcessIds,
    IReadOnlyList<NonNativeProcessInfo> SortedRows);
