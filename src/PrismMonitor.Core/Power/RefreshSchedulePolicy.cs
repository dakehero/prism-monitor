namespace PrismMonitor.Core.Power;

public static class RefreshSchedulePolicy
{
    private static readonly TimeSpan ResponsiveMainWindowRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BatteryMainWindowRefreshInterval = TimeSpan.FromSeconds(10);

    public static RefreshMode GetRefreshMode(PowerSource powerSource, bool isMainWindowVisible)
    {
        if (powerSource == PowerSource.Battery && !isMainWindowVisible)
        {
            return RefreshMode.InteractionOnly;
        }

        return RefreshMode.PeriodicBackground;
    }

    public static TimeSpan GetMainWindowRefreshInterval(PowerSource powerSource)
    {
        return powerSource == PowerSource.Battery
            ? BatteryMainWindowRefreshInterval
            : ResponsiveMainWindowRefreshInterval;
    }

    public static TimeSpan GetBackgroundRefreshInterval(PowerSource powerSource, bool isMainWindowVisible)
    {
        return powerSource == PowerSource.Battery && isMainWindowVisible
            ? BatteryMainWindowRefreshInterval
            : ResponsiveMainWindowRefreshInterval;
    }
}
