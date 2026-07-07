using System.Text.Json.Serialization;

namespace PrismMonitor.Core.History;

public sealed record LaunchHistorySummary(
    string ProcessName,
    string Architecture,
    int LaunchCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    string? LastExecutablePath,
    int LastProcessId = 0)
{
    [JsonIgnore]
    public bool IsIgnored { get; init; }
}
