using PrismMonitor.Core.Power;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class RefreshSchedulePolicyTests
{
    [TestMethod]
    public void UsesPeriodicBackgroundRefreshOnAcPower()
    {
        RefreshMode mode = RefreshSchedulePolicy.GetRefreshMode(PowerSource.AC, isMainWindowVisible: false);

        Assert.AreEqual(RefreshMode.PeriodicBackground, mode);
    }

    [TestMethod]
    public void UsesInteractionOnlyRefreshOnBatteryWhenMainWindowIsHidden()
    {
        RefreshMode mode = RefreshSchedulePolicy.GetRefreshMode(PowerSource.Battery, isMainWindowVisible: false);

        Assert.AreEqual(RefreshMode.InteractionOnly, mode);
    }

    [TestMethod]
    public void UsesPeriodicBackgroundRefreshOnBatteryWhenMainWindowIsVisible()
    {
        RefreshMode mode = RefreshSchedulePolicy.GetRefreshMode(PowerSource.Battery, isMainWindowVisible: true);

        Assert.AreEqual(RefreshMode.PeriodicBackground, mode);
    }

    [TestMethod]
    public void TreatsUnknownPowerAsPeriodicBackgroundRefresh()
    {
        RefreshMode mode = RefreshSchedulePolicy.GetRefreshMode(PowerSource.Unknown, isMainWindowVisible: false);

        Assert.AreEqual(RefreshMode.PeriodicBackground, mode);
    }
}
