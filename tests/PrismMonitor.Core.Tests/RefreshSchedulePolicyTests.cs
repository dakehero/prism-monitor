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
    public void UsesPeriodicFallbackRefreshOnBatteryWhenMainWindowIsHidden()
    {
        RefreshMode mode = RefreshSchedulePolicy.GetRefreshMode(PowerSource.Battery, isMainWindowVisible: false);

        Assert.AreEqual(RefreshMode.PeriodicBackground, mode);
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

    [TestMethod]
    public void UsesLowerFrequencyMainWindowRefreshOnBattery()
    {
        TimeSpan interval = RefreshSchedulePolicy.GetMainWindowRefreshInterval(PowerSource.Battery);

        Assert.AreEqual(TimeSpan.FromSeconds(10), interval);
    }

    [TestMethod]
    [DataRow(PowerSource.AC)]
    [DataRow(PowerSource.Unknown)]
    public void UsesResponsiveMainWindowRefreshWhenPowerIsNotBattery(PowerSource powerSource)
    {
        TimeSpan interval = RefreshSchedulePolicy.GetMainWindowRefreshInterval(powerSource);

        Assert.AreEqual(TimeSpan.FromSeconds(3), interval);
    }

    [TestMethod]
    public void UsesLowerFrequencyBackgroundRefreshOnBatteryWhenMainWindowIsVisible()
    {
        TimeSpan interval = RefreshSchedulePolicy.GetBackgroundRefreshInterval(
            PowerSource.Battery,
            isMainWindowVisible: true);

        Assert.AreEqual(TimeSpan.FromSeconds(10), interval);
    }

    [TestMethod]
    public void UsesResponsiveBackgroundRefreshWhenPeriodicRefreshIsAllowed()
    {
        TimeSpan interval = RefreshSchedulePolicy.GetBackgroundRefreshInterval(
            PowerSource.AC,
            isMainWindowVisible: false);

        Assert.AreEqual(TimeSpan.FromSeconds(3), interval);
    }

    [TestMethod]
    [DataRow(PowerSource.AC, false, 3)]
    [DataRow(PowerSource.AC, true, 3)]
    [DataRow(PowerSource.Unknown, false, 3)]
    [DataRow(PowerSource.Battery, true, 10)]
    [DataRow(PowerSource.Battery, false, 30)]
    public void GetRefreshInterval_UsesSingleAdaptiveSchedule(
        PowerSource powerSource,
        bool isMainWindowVisible,
        int expectedSeconds)
    {
        TimeSpan interval = RefreshSchedulePolicy.GetRefreshInterval(powerSource, isMainWindowVisible);

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), interval);
    }
}
