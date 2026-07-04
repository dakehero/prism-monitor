using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class CpuTimeFormatterTests
{
    [TestMethod]
    public void Format_ReturnsZeroSeconds_ForZero()
    {
        Assert.AreEqual("0s", CpuTimeFormatter.Format(TimeSpan.Zero));
    }

    [TestMethod]
    public void Format_ReturnsSeconds_ForLessThanOneMinute()
    {
        Assert.AreEqual("59s", CpuTimeFormatter.Format(TimeSpan.FromSeconds(59)));
    }

    [TestMethod]
    public void Format_ReturnsMinutesAndSeconds_ForLessThanOneHour()
    {
        Assert.AreEqual("1m 01s", CpuTimeFormatter.Format(TimeSpan.FromSeconds(61)));
    }

    [TestMethod]
    public void Format_ReturnsHoursMinutesAndSeconds_ForOneHourOrMore()
    {
        Assert.AreEqual("1h 01m 01s", CpuTimeFormatter.Format(TimeSpan.FromSeconds(3661)));
    }
}
