namespace PrismMonitor.Core.Power;

public static class RefreshSchedulePolicy
{
    public static RefreshMode GetRefreshMode(PowerSource powerSource, bool isMainWindowVisible)
    {
        if (powerSource == PowerSource.Battery && !isMainWindowVisible)
        {
            return RefreshMode.InteractionOnly;
        }

        return RefreshMode.PeriodicBackground;
    }
}
