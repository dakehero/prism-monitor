using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Tests;

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

    [TestMethod]
    public async Task AddRuleAsync_PersistsNonNameRuleWithoutLegacyIgnoredName()
    {
        IgnoredProcessStore store = CreateStore();
        AppIdentityRule rule = new(
            "AppleMusic",
            PackageIdentity: "AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa",
            Architecture: "ARM64EC",
            Targets: SuppressionTarget.Toast);

        await store.AddRuleAsync(rule);

        AppIdentityRule savedRule = (await store.GetRulesAsync()).Single();
        Assert.AreEqual("AppleMusic", savedRule.DisplayName);
        Assert.AreEqual("AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa", savedRule.PackageIdentity);
        Assert.IsEmpty(await store.GetIgnoredNamesAsync());
    }

    [TestMethod]
    public async Task RemoveRuleAsync_RemovesNonNameRule()
    {
        IgnoredProcessStore store = CreateStore();
        AppIdentityRule rule = new(
            "AppleMusic",
            PackageIdentity: "AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa",
            Targets: SuppressionTarget.Toast);
        await store.AddRuleAsync(rule);

        await store.RemoveRuleAsync(rule);

        Assert.IsEmpty(await store.GetRulesAsync());
    }

    [TestMethod]
    public async Task RemoveRuleAsync_RemovesLegacyNameRule()
    {
        IgnoredProcessStore store = CreateStore();
        await store.AddAsync("Chrome");
        AppIdentityRule rule = (await store.GetRulesAsync()).Single();

        await store.RemoveRuleAsync(rule);

        Assert.IsEmpty(await store.GetRulesAsync());
        Assert.IsEmpty(await store.GetIgnoredNamesAsync());
    }

    private IgnoredProcessStore CreateStore()
    {
        _temporaryDirectory ??= Path.Combine(Path.GetTempPath(), "PrismMonitorTests", Guid.NewGuid().ToString("N"));
        return new IgnoredProcessStore(Path.Combine(_temporaryDirectory, "ignored-processes.json"));
    }
}
