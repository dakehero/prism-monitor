using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Monitoring;

public sealed record MonitoringConfiguration(
    IReadOnlyList<AppIdentityRule> Rules,
    MonitoringSettings Settings)
{
    public static MonitoringConfiguration Default { get; } = new([], MonitoringSettings.Default);
}
