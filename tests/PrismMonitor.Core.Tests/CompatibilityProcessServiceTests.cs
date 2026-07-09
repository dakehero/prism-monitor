using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class CompatibilityProcessServiceTests
{
    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsRowsSortedByCpuTimeDescending()
    {
        CompatibilityProcessInfo[] rows =
        [
            new("small", 1, "x64", TimeSpan.FromSeconds(1)),
            new("large", 2, "x86", TimeSpan.FromSeconds(30)),
            new("medium", 3, "x64", TimeSpan.FromSeconds(10))
        ];
        CompatibilityProcessService service = new(new StaticProvider(rows));

        IReadOnlyList<CompatibilityProcessInfo> result = await service.GetCurrentProcessesAsync();

        CollectionAssert.AreEqual(
            new[] { "large", "medium", "small" },
            result.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsEmptyList_WhenProviderReturnsNoRows()
    {
        CompatibilityProcessService service = new(new StaticProvider([]));

        IReadOnlyList<CompatibilityProcessInfo> result = await service.GetCurrentProcessesAsync();

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsEmptyList_WhenProviderThrows()
    {
        CompatibilityProcessService service = new(new ThrowingProvider());

        IReadOnlyList<CompatibilityProcessInfo> result = await service.GetCurrentProcessesAsync();

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReusesRecentSnapshotWithinCacheWindow()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CountingProvider provider = new(
            [new CompatibilityProcessInfo("first", 1, "x64", TimeSpan.FromSeconds(1))],
            [new CompatibilityProcessInfo("second", 2, "x64", TimeSpan.FromSeconds(1))]);
        CompatibilityProcessService service = new(
            provider,
            cacheDuration: TimeSpan.FromSeconds(2),
            clock: () => now);

        IReadOnlyList<CompatibilityProcessInfo> first = await service.GetCurrentProcessesAsync();
        now = now.AddSeconds(1);
        IReadOnlyList<CompatibilityProcessInfo> second = await service.GetCurrentProcessesAsync();

        Assert.AreEqual(1, provider.CallCount);
        CollectionAssert.AreEqual(first.Select(process => process.Name).ToArray(), second.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_RefreshesSnapshotAfterCacheWindow()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CountingProvider provider = new(
            [new CompatibilityProcessInfo("first", 1, "x64", TimeSpan.FromSeconds(1))],
            [new CompatibilityProcessInfo("second", 2, "x64", TimeSpan.FromSeconds(1))]);
        CompatibilityProcessService service = new(
            provider,
            cacheDuration: TimeSpan.FromSeconds(2),
            clock: () => now);

        _ = await service.GetCurrentProcessesAsync();
        now = now.AddSeconds(3);
        IReadOnlyList<CompatibilityProcessInfo> second = await service.GetCurrentProcessesAsync();

        Assert.AreEqual(2, provider.CallCount);
        CollectionAssert.AreEqual(new[] { "second" }, second.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_CoalescesConcurrentRefreshes()
    {
        BlockingProvider provider = new([new CompatibilityProcessInfo("shared", 1, "x64", TimeSpan.FromSeconds(1))]);
        CompatibilityProcessService service = new(
            provider,
            cacheDuration: TimeSpan.FromSeconds(2),
            clock: () => DateTimeOffset.UnixEpoch);

        Task<IReadOnlyList<CompatibilityProcessInfo>> first = service.GetCurrentProcessesAsync();
        Task<IReadOnlyList<CompatibilityProcessInfo>> second = service.GetCurrentProcessesAsync();
        provider.Release();
        await Task.WhenAll(first, second);

        Assert.AreEqual(1, provider.CallCount);
        CollectionAssert.AreEqual(first.Result.Select(process => process.Name).ToArray(), second.Result.Select(process => process.Name).ToArray());
    }

    private sealed class StaticProvider(IReadOnlyList<CompatibilityProcessInfo> rows) : IProcessInfoProvider
    {
        public Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(rows);
        }
    }

    private sealed class ThrowingProvider : IProcessInfoProvider
    {
        public Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider failed");
        }
    }

    private sealed class CountingProvider(params IReadOnlyList<CompatibilityProcessInfo>[] snapshots) : IProcessInfoProvider
    {
        private readonly Queue<IReadOnlyList<CompatibilityProcessInfo>> _snapshots = new(snapshots);

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_snapshots.Dequeue());
        }
    }

    private sealed class BlockingProvider(IReadOnlyList<CompatibilityProcessInfo> snapshot) : IProcessInfoProvider
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            await _release.Task.WaitAsync(cancellationToken);
            return snapshot;
        }

        public void Release()
        {
            _release.TrySetResult();
        }
    }
}
