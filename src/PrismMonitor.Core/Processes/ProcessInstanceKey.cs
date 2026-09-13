namespace PrismMonitor.Core.Processes;

public readonly record struct ProcessInstanceKey(
    int ProcessId,
    DateTimeOffset IdentityTime,
    bool IsCreationTimeVerified);
