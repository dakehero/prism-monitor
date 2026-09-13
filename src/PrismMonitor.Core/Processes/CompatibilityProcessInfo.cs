namespace PrismMonitor.Core.Processes;

public sealed record CompatibilityProcessInfo(
    string Name,
    int ProcessId,
    string Architecture,
    TimeSpan? CpuTime,
    string? ExecutablePath = null,
    string? PackageIdentity = null,
    string? PublisherIdentity = null,
    DateTimeOffset? CreationTime = null,
    DateTimeOffset? DetectedAt = null,
    ProcessInstanceKey? InstanceKey = null,
    string? IconCacheKey = null,
    bool HasLimitedDetails = false);
