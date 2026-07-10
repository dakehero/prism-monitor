namespace PrismMonitor.Core.Processes;

public static class CpuTimeFormatter
{
    public static string Format(TimeSpan? cpuTime)
    {
        if (cpuTime is null)
        {
            return "Unavailable";
        }

        long totalSeconds = Math.Max(0, (long)cpuTime.Value.TotalSeconds);
        long hours = totalSeconds / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes:00}m {seconds:00}s";
        }

        if (minutes > 0)
        {
            return $"{minutes}m {seconds:00}s";
        }

        return $"{seconds}s";
    }
}
