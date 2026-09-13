namespace PrismMonitor.Core.Monitoring;

public enum MonitoringRefreshReason
{
    Periodic,
    Interaction,
    WindowVisible,
    Manual,
    PowerChanged,
    ConfigurationChanged
}
