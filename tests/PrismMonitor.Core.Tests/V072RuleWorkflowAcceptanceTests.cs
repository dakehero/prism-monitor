using PrismMonitor.Core.History;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class V072RuleWorkflowAcceptanceTests
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
    public async Task RunningProcessRuleSuppressesAndDeleteRestoresAllSurfaces()
    {
        IgnoredProcessStore store = CreateIgnoredProcessStore();
        CompatibilityProcessInfo process = CreateAppleMusicProcess();

        AppIdentityRule rule = AppIdentityRuleStore.CreateRuleForIdentity(
            AppIdentityRuleFilter.ToIdentity(process),
            SuppressionTarget.All);
        await store.AddRuleAsync(rule);

        MonitoringSnapshot suppressedSnapshot = BuildSnapshot([process], await store.GetRulesAsync());
        Assert.IsEmpty(suppressedSnapshot.Processes);
        Assert.IsEmpty(suppressedSnapshot.TrayProcesses);
        Assert.IsEmpty(suppressedSnapshot.NotifiableProcesses);
        Assert.IsEmpty(suppressedSnapshot.HistoryProcesses);

        await store.RemoveRuleAsync((await store.GetRulesAsync()).Single());

        MonitoringSnapshot restoredSnapshot = BuildSnapshot([process], await store.GetRulesAsync());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, restoredSnapshot.Processes.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, restoredSnapshot.TrayProcesses.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, restoredSnapshot.NotifiableProcesses.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, restoredSnapshot.HistoryProcesses.Select(item => item.Name).ToArray());
    }

    [TestMethod]
    public async Task HistoryRuleSuppressesExitedAppFromHistory()
    {
        LaunchHistoryStore historyStore = CreateHistoryStore();
        DateTimeOffset now = new(2026, 7, 10, 8, 0, 0, TimeSpan.Zero);
        await historyStore.AppendAsync(new LaunchHistoryEvent(
            "AppleMusic",
            "ARM64EC",
            420,
            @"C:\Program Files\WindowsApps\AppleMusic.exe",
            null,
            now,
            PackageIdentity: "AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa",
            PublisherIdentity: "CN=Apple Inc."));

        LaunchHistorySummary summary = (await historyStore.GetSummaryAsync(now.AddMinutes(1), [])).Single();
        AppIdentityRule rule = AppIdentityRuleStore.CreateRuleForIdentity(
            AppIdentityRuleFilter.ToIdentity(summary),
            SuppressionTarget.History);

        LaunchHistorySummary suppressedSummary = (await historyStore.GetSummaryWithRulesAsync(
            now.AddMinutes(1),
            [rule])).Single();

        Assert.IsTrue(suppressedSummary.IsIgnored);
        Assert.AreEqual("AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa", rule.PackageIdentity);
        Assert.AreEqual(SuppressionTarget.History, rule.Targets);
    }

    [TestMethod]
    public async Task EditingTargetsChangesOnlySelectedSurfaces()
    {
        IgnoredProcessStore store = CreateIgnoredProcessStore();
        CompatibilityProcessInfo process = CreateAppleMusicProcess();
        AppIdentityRule rule = AppIdentityRuleStore.CreateRuleForIdentity(
            AppIdentityRuleFilter.ToIdentity(process),
            SuppressionTarget.Toast);
        await store.AddRuleAsync(rule);

        MonitoringSnapshot toastSuppressedSnapshot = BuildSnapshot([process], await store.GetRulesAsync());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, toastSuppressedSnapshot.Processes.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, toastSuppressedSnapshot.TrayProcesses.Select(item => item.Name).ToArray());
        Assert.IsEmpty(toastSuppressedSnapshot.NotifiableProcesses);
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, toastSuppressedSnapshot.HistoryProcesses.Select(item => item.Name).ToArray());

        await store.UpdateRuleTargetsAsync((await store.GetRulesAsync()).Single(), SuppressionTarget.Tray);

        MonitoringSnapshot traySuppressedSnapshot = BuildSnapshot([process], await store.GetRulesAsync());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, traySuppressedSnapshot.Processes.Select(item => item.Name).ToArray());
        Assert.IsEmpty(traySuppressedSnapshot.TrayProcesses);
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, traySuppressedSnapshot.NotifiableProcesses.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "AppleMusic" }, traySuppressedSnapshot.HistoryProcesses.Select(item => item.Name).ToArray());
    }

    [TestMethod]
    public async Task LegacyIgnoredNameMigratesIntoAllSurfaceRule()
    {
        string ignoredNamesPath = Path.Combine(TemporaryDirectory, "ignored-processes.json");
        await File.WriteAllTextAsync(ignoredNamesPath, """["Chrome"]""");
        IgnoredProcessStore store = new(ignoredNamesPath);

        AppIdentityRule rule = (await store.GetRulesAsync()).Single();

        Assert.AreEqual("Chrome", rule.ProcessName);
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

    private IgnoredProcessStore CreateIgnoredProcessStore()
    {
        return new IgnoredProcessStore(Path.Combine(TemporaryDirectory, "ignored-processes.json"));
    }

    private LaunchHistoryStore CreateHistoryStore()
    {
        return new LaunchHistoryStore(
            Path.Combine(TemporaryDirectory, "launch-events.jsonl"),
            Path.Combine(TemporaryDirectory, "launch-summary.json"));
    }

    private static MonitoringSnapshot BuildSnapshot(
        IReadOnlyList<CompatibilityProcessInfo> processes,
        IReadOnlyList<AppIdentityRule> rules)
    {
        return MonitoringSnapshotBuilder.Build(processes, rules, MonitoringSettings.Default);
    }

    private static CompatibilityProcessInfo CreateAppleMusicProcess()
    {
        return new CompatibilityProcessInfo(
            "AppleMusic",
            420,
            "ARM64EC",
            TimeSpan.FromSeconds(9),
            ExecutablePath: @"C:\Program Files\WindowsApps\AppleMusic.exe",
            PackageIdentity: "AppleInc.AppleMusic_1.0.0.0_arm64__nzyj5cx40ttqa",
            PublisherIdentity: "CN=Apple Inc.");
    }
}
