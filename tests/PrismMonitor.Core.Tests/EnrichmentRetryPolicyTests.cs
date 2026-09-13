using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class EnrichmentRetryPolicyTests
{
    [TestMethod]
    [DataRow(1, 30)]
    [DataRow(2, 60)]
    [DataRow(3, 120)]
    [DataRow(4, 240)]
    [DataRow(5, 300)]
    [DataRow(12, 300)]
    public void GetDelay_UsesCappedExponentialBackoff(int failureCount, int expectedSeconds)
    {
        Assert.AreEqual(
            TimeSpan.FromSeconds(expectedSeconds),
            EnrichmentRetryPolicy.GetDelay(failureCount));
    }
}
