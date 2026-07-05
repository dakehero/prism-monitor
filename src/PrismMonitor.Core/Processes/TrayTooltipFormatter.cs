namespace PrismMonitor.Core.Processes;

public static class TrayTooltipFormatter
{
    public static string FormatSummary(IEnumerable<CompatibilityProcessInfo> processes)
    {
        int count = processes.Count();
        return count == 0
            ? "No compatibility-mode processes"
            : CultureInvariant($"{count} compatibility-mode {(count == 1 ? "process" : "processes")}");
    }

    public static string FormatTopProcesses(IEnumerable<CompatibilityProcessInfo> processes, int topN)
    {
        List<CompatibilityProcessInfo> topProcesses = processes
            .OrderByDescending(process => process.CpuTime)
            .Take(Math.Max(0, topN))
            .ToList();

        if (topProcesses.Count == 0)
        {
            return "No compatibility-mode processes";
        }

        return string.Join(Environment.NewLine, topProcesses.Select(GetDisplayName));
    }

    private static string GetDisplayName(CompatibilityProcessInfo process)
    {
        string name = string.IsNullOrWhiteSpace(process.Name)
            ? CultureInvariant($"PID {process.ProcessId}")
            : process.Name;

        return string.IsNullOrWhiteSpace(process.Architecture)
            ? name
            : CultureInvariant($"{name} ({process.Architecture})");
    }

    private static string CultureInvariant(FormattableString value)
    {
        return FormattableString.Invariant(value);
    }
}
