using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Processes;

public static class IgnoredProcessFilter
{
    public static IReadOnlyList<CompatibilityProcessInfo> Filter(
        IEnumerable<CompatibilityProcessInfo> processes,
        IEnumerable<string> ignoredNames)
    {
        HashSet<string> normalizedIgnoredNames = ignoredNames
            .Select(NormalizeName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedIgnoredNames.Count == 0)
        {
            return processes.ToList();
        }

        return processes
            .Where(process => !normalizedIgnoredNames.Contains(NormalizeName(process.Name)))
            .ToList();
    }

    public static string NormalizeName(string? processName)
    {
        return AppIdentityRuleNormalizer.NormalizeProcessName(processName);
    }
}
