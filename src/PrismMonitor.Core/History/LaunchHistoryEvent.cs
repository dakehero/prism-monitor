using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.History;

public sealed record LaunchHistoryEvent(
    string ProcessName,
    string Architecture,
    int ProcessId,
    string? ExecutablePath,
    DateTimeOffset? StartedAt,
    DateTimeOffset DetectedAt,
    string? PackageIdentity = null,
    string? PublisherIdentity = null,
    ProcessInstanceKey? InstanceKey = null);
