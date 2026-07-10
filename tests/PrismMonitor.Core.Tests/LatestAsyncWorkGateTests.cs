using PrismMonitor.Core.Ui;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class LatestAsyncWorkGateTests
{
    [TestMethod]
    public async Task RunAsync_SerializesWorkAndSkipsSupersededPendingValues()
    {
        LatestAsyncWorkGate<int> gate = new();
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> appliedValues = [];
        int activeOperations = 0;
        int maximumConcurrency = 0;

        async Task ApplyAsync(int value)
        {
            int concurrency = Interlocked.Increment(ref activeOperations);
            maximumConcurrency = Math.Max(maximumConcurrency, concurrency);
            appliedValues.Add(value);

            if (value == 1)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
            }

            Interlocked.Decrement(ref activeOperations);
        }

        Task first = gate.RunAsync(1, ApplyAsync);
        await firstStarted.Task;
        Task superseded = gate.RunAsync(2, ApplyAsync);
        Task latest = gate.RunAsync(3, ApplyAsync);

        releaseFirst.SetResult();
        await Task.WhenAll(first, superseded, latest);

        CollectionAssert.AreEqual(new[] { 1, 3 }, appliedValues);
        Assert.AreEqual(1, maximumConcurrency);
    }
}
