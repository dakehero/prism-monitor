namespace PrismMonitor.Core.Processes;

public sealed record ProcessObservation(
    int ProcessId,
    string Name,
    TimeSpan? CpuTime,
    DateTimeOffset? CreationTime = null);
