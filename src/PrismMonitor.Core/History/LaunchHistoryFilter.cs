namespace PrismMonitor.Core.History;

public static class LaunchHistoryFilter
{
    public static IReadOnlyList<LaunchHistorySummary> Apply(
        IReadOnlyList<LaunchHistorySummary> summaries,
        LaunchHistoryQuery query)
    {
        IEnumerable<LaunchHistorySummary> filtered = summaries;

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string text = query.Text.Trim();
            filtered = filtered.Where(summary =>
                summary.ProcessName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (summary.LastExecutablePath?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(query.Architecture))
        {
            string architecture = query.Architecture.Trim();
            filtered = filtered.Where(summary => string.Equals(
                summary.Architecture,
                architecture,
                StringComparison.OrdinalIgnoreCase));
        }

        filtered = query.IgnoredState switch
        {
            LaunchHistoryIgnoredState.IgnoredOnly => filtered.Where(summary => summary.IsIgnored),
            LaunchHistoryIgnoredState.NotIgnoredOnly => filtered.Where(summary => !summary.IsIgnored),
            _ => filtered
        };

        return filtered
            .OrderByDescending(summary => summary.LastSeenAt)
            .ThenBy(summary => summary.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(summary => summary.ProcessName, StringComparer.Ordinal)
            .ToList();
    }
}
