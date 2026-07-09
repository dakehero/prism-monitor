using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Monitoring;

public static class MonitoringSnapshotBuilder
{
    public static MonitoringSnapshot Build(
        IReadOnlyList<CompatibilityProcessInfo> processes,
        IReadOnlyList<AppIdentityRule> rules,
        MonitoringSettings settings)
    {
        IReadOnlyList<CompatibilityProcessInfo> includedProcesses =
            ArchitectureProcessFilter.FilterVisibleProcesses(processes, settings);
        IReadOnlyList<CompatibilityProcessInfo> notifiableProcesses =
            ArchitectureProcessFilter.FilterNotifiableProcesses(includedProcesses, settings);

        return new MonitoringSnapshot(
            AppIdentityRuleFilter.FilterProcesses(includedProcesses, rules, SuppressionTarget.Processes),
            AppIdentityRuleFilter.FilterProcesses(includedProcesses, rules, SuppressionTarget.Tray),
            AppIdentityRuleFilter.FilterProcesses(notifiableProcesses, rules, SuppressionTarget.Toast),
            AppIdentityRuleFilter.FilterProcesses(includedProcesses, rules, SuppressionTarget.History));
    }
}
