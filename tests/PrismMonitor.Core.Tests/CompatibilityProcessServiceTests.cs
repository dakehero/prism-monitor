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
}
