namespace PrismMonitor.Core.Monitoring;

public sealed record MonitoringCycleDiagnostics(
    bool Succeeded,
    MonitoringRefreshReason Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int ProviderCallCount,
    int ObservedProcessCount,
    int CompatibleProcessCount,
    int CacheHits,
    int CacheMisses,
    int CacheRetries,
    int CachePrunes,
    string? Error = null)
{
    public TimeSpan Duration => CompletedAt - StartedAt;

    public long TotalProviderCallCount { get; init; }

    public long TotalCacheHits { get; init; }

    public long TotalCacheMisses { get; init; }

    public long TotalCacheRetries { get; init; }

    public long TotalCachePrunes { get; init; }

    public static MonitoringCycleDiagnostics Empty { get; } = new(
        Succeeded: false,
        MonitoringRefreshReason.Periodic,
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue,
        ProviderCallCount: 0,
        ObservedProcessCount: 0,
        CompatibleProcessCount: 0,
        CacheHits: 0,
        CacheMisses: 0,
        CacheRetries: 0,
        CachePrunes: 0);
}
