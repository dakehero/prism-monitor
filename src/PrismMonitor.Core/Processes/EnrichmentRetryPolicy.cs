namespace PrismMonitor.Core.Processes;

public static class EnrichmentRetryPolicy
{
    public static TimeSpan GetDelay(int failureCount)
    {
        int exponent = Math.Clamp(failureCount - 1, 0, 4);
        int seconds = Math.Min(30 * (1 << exponent), 300);
        return TimeSpan.FromSeconds(seconds);
    }
}
