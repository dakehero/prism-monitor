using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class IgnoredProcessStoreTests
{
    private string? _temporaryDirectory;

    [TestCleanup]
    public void Cleanup()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task AddAsync_NormalizesSortsAndPersistsNames()
    {
        IgnoredProcessStore store = CreateStore();

        await store.AddAsync("Chrome.EXE");
        await store.AddAsync("foo");

        IReadOnlyList<string> names = await CreateStore().GetIgnoredNamesAsync();

        CollectionAssert.AreEqual(new[] { "Chrome", "foo" }, names.ToArray());
    }

    [TestMethod]
    public async Task AddAsync_IgnoresDuplicateNamesCaseInsensitively()
    {
        IgnoredProcessStore store = CreateStore();

        await store.AddAsync("chrome");
        await store.AddAsync("CHROME.EXE");

        IReadOnlyList<string> names = await store.GetIgnoredNamesAsync();

        CollectionAssert.AreEqual(new[] { "chrome" }, names.ToArray());
    }

    [TestMethod]
    public async Task RemoveAsync_RemovesNameCaseInsensitively()
    {
        IgnoredProcessStore store = CreateStore();
        await store.AddAsync("chrome");
        await store.AddAsync("legacy");

        await store.RemoveAsync("CHROME.EXE");

        IReadOnlyList<string> names = await store.GetIgnoredNamesAsync();
        CollectionAssert.AreEqual(new[] { "legacy" }, names.ToArray());
    }

    private IgnoredProcessStore CreateStore()
    {
        _temporaryDirectory ??= Path.Combine(Path.GetTempPath(), "NativeGuardTests", Guid.NewGuid().ToString("N"));
        return new IgnoredProcessStore(Path.Combine(_temporaryDirectory, "ignored-processes.json"));
    }
}
