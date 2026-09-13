namespace PrismMonitor.Core.Processes;

public sealed record ProcessSnapshotInfo(
    int ProcessId,
    string Name,
    TimeSpan? CpuTime,
    DateTimeOffset? CreationTime,
    DateTimeOffset DetectedAt,
    ProcessInstanceKey InstanceKey);
