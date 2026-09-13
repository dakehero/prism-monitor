namespace PrismMonitor.Core.Power;

public static class RefreshSchedulePolicy
{
    private static readonly TimeSpan ResponsiveRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BatteryMainWindowRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BatteryHiddenRefreshInterval = TimeSpan.FromSeconds(30);

    public static RefreshMode GetRefreshMode(PowerSource powerSource, bool isMainWindowVisible)
    {
        return RefreshMode.PeriodicBackground;
    }

    public static TimeSpan GetRefreshInterval(PowerSource powerSource, bool isMainWindowVisible)
    {
        if (powerSource != PowerSource.Battery)
        {
            return ResponsiveRefreshInterval;
        }

        return isMainWindowVisible
            ? BatteryMainWindowRefreshInterval
            : BatteryHiddenRefreshInterval;
    }

    public static TimeSpan GetMainWindowRefreshInterval(PowerSource powerSource)
    {
        return GetRefreshInterval(powerSource, isMainWindowVisible: true);
    }

    public static TimeSpan GetBackgroundRefreshInterval(PowerSource powerSource, bool isMainWindowVisible)
    {
        return GetRefreshInterval(powerSource, isMainWindowVisible);
    }
}
