using System.Text.Json;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class AppIdentityRuleStoreTests
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
    public async Task GetRulesAsync_MigratesLegacyIgnoredNamesToAllSurfaceRules()
    {
        string legacyPath = Path.Combine(TemporaryDirectory, "ignored-processes.json");
        await File.WriteAllTextAsync(
            legacyPath,
            JsonSerializer.Serialize(new[] { "Chrome.EXE", "legacy" }));
        AppIdentityRuleStore store = CreateStore();

        IReadOnlyList<AppIdentityRule> rules = await store.GetRulesAsync();

        CollectionAssert.AreEqual(new[] { "Chrome", "legacy" }, rules.Select(rule => rule.ProcessName).ToArray());
        Assert.IsTrue(rules.All(rule => rule.Targets == SuppressionTarget.All));
        Assert.IsTrue(File.Exists(Path.Combine(TemporaryDirectory, "app-rules.json")));
    }

    [TestMethod]
    public async Task GetRulesAsync_WritesVersionedRuleDocument()
    {
        AppIdentityRuleStore store = CreateStore();

        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.All);

        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(TemporaryDirectory, "app-rules.json")));
        Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.GetProperty("rules").ValueKind);
    }

    [TestMethod]
    public async Task GetRulesAsync_RewritesBareArrayRulesAsVersionedDocument()
    {
        string rulesPath = Path.Combine(TemporaryDirectory, "app-rules.json");
        await File.WriteAllTextAsync(
            rulesPath,
            """[{"displayName":"Chrome","processName":"Chrome","targets":15}]""");

        IReadOnlyList<AppIdentityRule> rules = await CreateStore().GetRulesAsync();

        Assert.HasCount(1, rules);
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(rulesPath));
        Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.AreEqual(JsonValueKind.Array, document.RootElement.GetProperty("rules").ValueKind);
    }

    [TestMethod]
    public async Task GetRulesAsync_RewritesEmptyBareArrayRulesAsVersionedDocument()
    {
        string rulesPath = Path.Combine(TemporaryDirectory, "app-rules.json");
        await File.WriteAllTextAsync(rulesPath, "[]");

        IReadOnlyList<AppIdentityRule> rules = await CreateStore().GetRulesAsync();

        Assert.IsEmpty(rules);
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(rulesPath));
        Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.AreEqual(0, document.RootElement.GetProperty("rules").GetArrayLength());
    }

    [TestMethod]
    public async Task GetRulesAsync_MergesLegacyNamesWhenRuleFileAlreadyExists()
    {
        AppIdentityRuleStore store = CreateStore();
        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.All);
        await File.WriteAllTextAsync(
            Path.Combine(TemporaryDirectory, "ignored-processes.json"),
            JsonSerializer.Serialize(new[] { "Legacy.exe" }));

        IReadOnlyList<AppIdentityRule> rules = await store.GetRulesAsync();

        CollectionAssert.AreEqual(new[] { "Chrome", "Legacy" }, rules.Select(rule => rule.ProcessName).ToArray());
    }

    [TestMethod]
    public async Task AddProcessNameRuleAsync_NormalizesSortsAndDeduplicatesRules()
    {
        AppIdentityRuleStore store = CreateStore();

        await store.AddProcessNameRuleAsync("Legacy.EXE", SuppressionTarget.All);
        await store.AddProcessNameRuleAsync("legacy", SuppressionTarget.All);
        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.Toast);

        IReadOnlyList<AppIdentityRule> rules = await CreateStore().GetRulesAsync();

        CollectionAssert.AreEqual(new[] { "Chrome", "Legacy" }, rules.Select(rule => rule.ProcessName).ToArray());
        Assert.AreEqual(SuppressionTarget.Toast, rules[0].Targets);
        Assert.AreEqual(SuppressionTarget.All, rules[1].Targets);
    }

    [TestMethod]
    public async Task RemoveProcessNameRuleAsync_RemovesNameCaseInsensitively()
    {
        AppIdentityRuleStore store = CreateStore();
        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.All);
        await store.AddProcessNameRuleAsync("Legacy", SuppressionTarget.All);

        await store.RemoveProcessNameRuleAsync("chrome.exe");

        IReadOnlyList<AppIdentityRule> rules = await store.GetRulesAsync();
        CollectionAssert.AreEqual(new[] { "Legacy" }, rules.Select(rule => rule.ProcessName).ToArray());
    }

    [TestMethod]
    public async Task RemoveProcessNameRuleAsync_KeepsTargetSpecificNameRules()
    {
        AppIdentityRuleStore store = CreateStore();
        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.All);
        await store.AddProcessNameRuleAsync("Chrome", SuppressionTarget.Toast);

        await store.RemoveProcessNameRuleAsync("chrome");

        IReadOnlyList<AppIdentityRule> rules = await store.GetRulesAsync();
        Assert.HasCount(1, rules);
        Assert.AreEqual(SuppressionTarget.Toast, rules[0].Targets);
    }

    [TestMethod]
    public async Task AddOrUpdateRuleAsync_UsesStrongestAvailableIdentity()
    {
        AppIdentityRuleStore store = CreateStore();
        AppIdentity identity = new(
            "AppleMusic",
            ExecutablePath: @"C:\Program Files\WindowsApps\AppleMusic.exe",
            PackageIdentity: "AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa",
            PublisherIdentity: "CN=Apple Inc.",
            Architecture: "ARM64EC");

        await store.AddOrUpdateRuleAsync(
            AppIdentityRuleStore.CreateRuleForIdentity(identity, SuppressionTarget.Toast));

        AppIdentityRule rule = (await store.GetRulesAsync()).Single();
        Assert.AreEqual("AppleMusic", rule.DisplayName);
        Assert.IsNull(rule.ProcessName);
        Assert.IsNull(rule.ExecutablePath);
        Assert.AreEqual("AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa", rule.PackageIdentity);
        Assert.IsNull(rule.PublisherIdentity);
        Assert.AreEqual("ARM64EC", rule.Architecture);
        Assert.AreEqual(SuppressionTarget.Toast, rule.Targets);
    }

    [TestMethod]
    public async Task AddOrUpdateRuleAsync_ReplacesRuleWithSameIdentityAndTargets()
    {
        AppIdentityRuleStore store = CreateStore();
        await store.AddOrUpdateRuleAsync(new AppIdentityRule(
            "Chrome",
            ExecutablePath: @"C:\Apps\Chrome.exe",
            Targets: SuppressionTarget.Toast));

        await store.AddOrUpdateRuleAsync(new AppIdentityRule(
            "Google Chrome",
            ExecutablePath: @"c:\apps\chrome.exe",
            Targets: SuppressionTarget.Toast));

        AppIdentityRule rule = (await store.GetRulesAsync()).Single();
        Assert.AreEqual("Google Chrome", rule.DisplayName);
    }

    [TestMethod]
    public async Task AddOrUpdateRuleAsync_DiscardsArchitectureOnlyRules()
    {
        AppIdentityRuleStore store = CreateStore();

        await store.AddOrUpdateRuleAsync(new AppIdentityRule(
            "Architecture only",
            Architecture: "x64",
            Targets: SuppressionTarget.All));

        Assert.IsEmpty(await store.GetRulesAsync());
    }

    [TestMethod]
    public async Task RemoveRuleAsync_RemovesMatchingRuleOnly()
    {
        AppIdentityRuleStore store = CreateStore();
        AppIdentityRule allRule = new("Chrome", ProcessName: "Chrome", Targets: SuppressionTarget.All);
        AppIdentityRule toastRule = new("Chrome toast", ProcessName: "Chrome", Targets: SuppressionTarget.Toast);
        await store.AddOrUpdateRuleAsync(allRule);
        await store.AddOrUpdateRuleAsync(toastRule);

        await store.RemoveRuleAsync(toastRule);

        AppIdentityRule rule = (await store.GetRulesAsync()).Single();
        Assert.AreEqual(SuppressionTarget.All, rule.Targets);
    }

    private string TemporaryDirectory
    {
        get
        {
            _temporaryDirectory ??= Path.Combine(Path.GetTempPath(), "PrismMonitorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            return _temporaryDirectory;
        }
    }

    private AppIdentityRuleStore CreateStore()
    {
        return new AppIdentityRuleStore(
            Path.Combine(TemporaryDirectory, "app-rules.json"),
            Path.Combine(TemporaryDirectory, "ignored-processes.json"));
    }
}
