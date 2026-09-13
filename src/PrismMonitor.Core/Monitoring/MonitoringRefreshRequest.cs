namespace PrismMonitor.Core.Monitoring;

public sealed record MonitoringRefreshRequest(
    MonitoringRefreshReason Reason,
    bool RequestFullDetails = false)
{
    public static MonitoringRefreshRequest Periodic { get; } =
        new(MonitoringRefreshReason.Periodic);
}
