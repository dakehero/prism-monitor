using PrismMonitor.Core.History;
using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Rules;

public static class AppIdentityRuleFilter
{
    public static IReadOnlyList<CompatibilityProcessInfo> FilterProcesses(
        IEnumerable<CompatibilityProcessInfo> processes,
        IEnumerable<AppIdentityRule> rules,
        SuppressionTarget target)
    {
        AppIdentityRule[] ruleArray = rules.ToArray();
        if (ruleArray.Length == 0)
        {
            return processes.ToList();
        }

        return processes
            .Where(process => !AppIdentityRuleEvaluator.IsSuppressed(ToIdentity(process), ruleArray, target))
            .ToList();
    }

    public static IReadOnlyList<LaunchHistorySummary> ApplyHistoryState(
        IEnumerable<LaunchHistorySummary> summaries,
        IEnumerable<AppIdentityRule> rules)
    {
        AppIdentityRule[] ruleArray = rules.ToArray();
        return summaries
            .Select(summary => summary with
            {
                IsIgnored = AppIdentityRuleEvaluator.IsSuppressed(ToIdentity(summary), ruleArray, SuppressionTarget.History)
            })
            .ToList();
    }

    public static AppIdentity ToIdentity(CompatibilityProcessInfo process)
    {
        return new AppIdentity(
            process.Name,
            process.ExecutablePath,
            process.PackageIdentity,
            process.PublisherIdentity,
            Architecture: process.Architecture);
    }

    public static AppIdentity ToIdentity(LaunchHistorySummary summary)
    {
        return new AppIdentity(
            summary.ProcessName,
            summary.LastExecutablePath,
            summary.PackageIdentity,
            summary.PublisherIdentity,
            Architecture: summary.Architecture);
    }
}
