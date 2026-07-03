using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class NonNativeProcessServiceTests
{
    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsRowsSortedByCpuTimeDescending()
    {
        NonNativeProcessInfo[] rows =
        [
            new("small", 1, "x64", TimeSpan.FromSeconds(1)),
            new("large", 2, "x86", TimeSpan.FromSeconds(30)),
            new("medium", 3, "x64", TimeSpan.FromSeconds(10))
        ];
        NonNativeProcessService service = new(new StaticProvider(rows));

        IReadOnlyList<NonNativeProcessInfo> result = await service.GetCurrentProcessesAsync();

        CollectionAssert.AreEqual(
            new[] { "large", "medium", "small" },
            result.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsEmptyList_WhenProviderReturnsNoRows()
    {
        NonNativeProcessService service = new(new StaticProvider([]));

        IReadOnlyList<NonNativeProcessInfo> result = await service.GetCurrentProcessesAsync();

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetCurrentProcessesAsync_ReturnsEmptyList_WhenProviderThrows()
    {
        NonNativeProcessService service = new(new ThrowingProvider());

        IReadOnlyList<NonNativeProcessInfo> result = await service.GetCurrentProcessesAsync();

        Assert.IsEmpty(result);
    }

    private sealed class StaticProvider(IReadOnlyList<NonNativeProcessInfo> rows) : IProcessInfoProvider
    {
        public Task<IReadOnlyList<NonNativeProcessInfo>> GetNonNativeProcessesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(rows);
        }
    }

    private sealed class ThrowingProvider : IProcessInfoProvider
    {
        public Task<IReadOnlyList<NonNativeProcessInfo>> GetNonNativeProcessesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("provider failed");
        }
    }
}
