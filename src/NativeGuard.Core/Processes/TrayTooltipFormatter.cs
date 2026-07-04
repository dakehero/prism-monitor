namespace NativeGuard.Core.Processes;

public static class TrayTooltipFormatter
{
    public static string FormatTopProcesses(IEnumerable<NonNativeProcessInfo> processes, int topN)
    {
        List<NonNativeProcessInfo> topProcesses = processes
            .OrderByDescending(process => process.CpuTime)
            .Take(Math.Max(0, topN))
            .ToList();

        if (topProcesses.Count == 0)
        {
            return "没有非原生进程";
        }

        return string.Join(Environment.NewLine, topProcesses.Select(GetDisplayName));
    }

    private static string GetDisplayName(NonNativeProcessInfo process)
    {
        return string.IsNullOrWhiteSpace(process.Name)
            ? CultureInvariant($"PID {process.ProcessId}")
            : process.Name;
    }

    private static string CultureInvariant(FormattableString value)
    {
        return FormattableString.Invariant(value);
    }
}
