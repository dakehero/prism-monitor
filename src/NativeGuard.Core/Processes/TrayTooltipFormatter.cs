using System.Text;

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
            return "Native Guard: no non-native processes";
        }

        StringBuilder builder = new();
        builder.Append(CultureInvariant($"Native Guard: Top {topProcesses.Count}"));

        foreach (NonNativeProcessInfo process in topProcesses)
        {
            builder.AppendLine();
            builder.Append(CultureInvariant(
                $"{GetDisplayName(process)} #{process.ProcessId} {process.Architecture} {CpuTimeFormatter.Format(process.CpuTime)}"));
        }

        return builder.ToString();
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
