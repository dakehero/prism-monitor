using PrismMonitor.Core.History;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class LaunchHistoryStoreTests
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
    public async Task AppendAsync_WritesJsonlAndUpdatesSummary()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(new LaunchHistoryEvent("Chrome", "x64", 100, @"C:\Apps\Chrome.exe", now.AddSeconds(-5), now));
        await store.AppendAsync(new LaunchHistoryEvent("chrome", "x64", 101, @"C:\Apps\Chrome.exe", now.AddSeconds(-1), now.AddMinutes(1)));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now.AddMinutes(2), []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("Chrome", summaries[0].ProcessName);
        Assert.AreEqual("x64", summaries[0].Architecture);
        Assert.AreEqual(2, summaries[0].LaunchCount);
        Assert.AreEqual(@"C:\Apps\Chrome.exe", summaries[0].LastExecutablePath);
        Assert.AreEqual(101, summaries[0].LastProcessId);
    }

    [TestMethod]
    public async Task AppendRangeAsync_WritesOneCycleAsJsonlAndUpdatesSummary()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);

        await store.AppendRangeAsync(
        [
            new LaunchHistoryEvent("One", "x64", 100, null, now.AddSeconds(-1), now),
            new LaunchHistoryEvent("Two", "x86", 101, null, now.AddSeconds(-1), now)
        ]);

        string eventsPath = Path.Combine(_temporaryDirectory!, "launch-events.jsonl");
        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now.AddMinutes(1), []);
        Assert.HasCount(2, File.ReadAllLines(eventsPath));
        Assert.HasCount(2, summaries);
    }

    [TestMethod]
    public async Task GetSummaryAsync_PrunesEventsOlderThanThirtyDays()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(new LaunchHistoryEvent("OldApp", "x86", 10, null, null, now.AddDays(-31)));
        await store.AppendAsync(new LaunchHistoryEvent("NewApp", "x64", 11, null, null, now.AddDays(-2)));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now, []);

        CollectionAssert.AreEqual(new[] { "NewApp" }, summaries.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public async Task GetSummaryAsync_RebuildsStaleSummaryCountsOutsideRetentionWindow()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset olderEventTime = now.AddDays(-31);
        DateTimeOffset newerEventTime = now.AddDays(-1);

        await store.AppendAsync(new LaunchHistoryEvent("Chrome", "x64", 100, null, null, olderEventTime));
        await store.AppendAsync(new LaunchHistoryEvent("chrome", "x64", 101, null, null, newerEventTime));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now, []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual(1, summaries[0].LaunchCount);
        Assert.AreEqual(newerEventTime, summaries[0].FirstSeenAt);
        Assert.AreEqual(newerEventTime, summaries[0].LastSeenAt);
    }

    [TestMethod]
    public async Task GetSummaryAsync_SkipsMalformedJsonlLines()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_temporaryDirectory!);
        await File.WriteAllLinesAsync(Path.Combine(_temporaryDirectory!, "launch-events.jsonl"), [
            "{not valid json",
            """{"processName":"Valid","architecture":"x64","processId":42,"executablePath":null,"startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}"""
        ]);

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now, []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("Valid", summaries[0].ProcessName);
    }

    [TestMethod]
    public async Task GetSummaryAsync_ReadsExistingSummaryFileWhenPresent()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        await store.AppendAsync(new LaunchHistoryEvent("CachedApp", "x64", 42, @"C:\Apps\CachedApp.exe", null, now));
        File.Delete(Path.Combine(_temporaryDirectory!, "launch-events.jsonl"));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now.AddMinutes(1), []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("CachedApp", summaries[0].ProcessName);
        Assert.AreEqual("x64", summaries[0].Architecture);
        Assert.AreEqual(1, summaries[0].LaunchCount);
        Assert.AreEqual(@"C:\Apps\CachedApp.exe", summaries[0].LastExecutablePath);
    }

    [TestMethod]
    public async Task GetSummaryAsync_RebuildsOldSummaryWhenEventsContainRicherIdentity()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_temporaryDirectory!);
        await File.WriteAllTextAsync(
            Path.Combine(_temporaryDirectory!, "launch-summary.json"),
            """
            [{"processName":"Tool","architecture":"x64","launchCount":2,"firstSeenAt":"2026-07-06T08:00:00+00:00","lastSeenAt":"2026-07-06T08:01:00+00:00","lastExecutablePath":"C:\\Other\\Tool.exe","lastProcessId":101}]
            """);
        await File.WriteAllLinesAsync(Path.Combine(_temporaryDirectory!, "launch-events.jsonl"), [
            """{"processName":"Tool","architecture":"x64","processId":100,"executablePath":"C:\\Apps\\Tool.exe","startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}""",
            """{"processName":"Tool","architecture":"x64","processId":101,"executablePath":"C:\\Other\\Tool.exe","startedAt":null,"detectedAt":"2026-07-06T08:01:00+00:00"}"""
        ]);

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now.AddMinutes(2), []);

        Assert.HasCount(2, summaries);
        CollectionAssert.AreEquivalent(
            new[] { @"C:\Apps\Tool.exe", @"C:\Other\Tool.exe" },
            summaries.Select(summary => summary.LastExecutablePath).ToArray());
    }

    [TestMethod]
    public async Task GetSummaryAsync_RebuildsSummaryMissingLastProcessId()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_temporaryDirectory!);
        await File.WriteAllTextAsync(
            Path.Combine(_temporaryDirectory!, "launch-summary.json"),
            """
            [{"processName":"CachedApp","architecture":"x64","launchCount":1,"firstSeenAt":"2026-07-06T08:00:00+00:00","lastSeenAt":"2026-07-06T08:00:00+00:00","lastExecutablePath":"C:\\Apps\\CachedApp.exe"}]
            """);
        await File.WriteAllTextAsync(
            Path.Combine(_temporaryDirectory!, "launch-events.jsonl"),
            """
            {"processName":"CachedApp","architecture":"x64","processId":88,"executablePath":"C:\\Apps\\CachedApp.exe","startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}

            """);

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now.AddMinutes(1), []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual(88, summaries[0].LastProcessId);
    }

    [TestMethod]
    public async Task GetSummaryAsync_SkipsShapeCorruptJsonlLines()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_temporaryDirectory!);
        await File.WriteAllLinesAsync(Path.Combine(_temporaryDirectory!, "launch-events.jsonl"), [
            """{"processName":"MissingArchitecture","architecture":null,"processId":40,"executablePath":null,"startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}""",
            """{"architecture":"x64","processId":41,"executablePath":null,"startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}""",
            """{"processName":"Valid","architecture":"x64","processId":42,"executablePath":null,"startedAt":null,"detectedAt":"2026-07-06T08:00:00+00:00"}"""
        ]);

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now, []);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("Valid", summaries[0].ProcessName);
    }

    [TestMethod]
    public async Task ClearAsync_RemovesEventsAndSummary()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        await store.AppendAsync(new LaunchHistoryEvent("Chrome", "x64", 100, null, null, now));

        await store.ClearAsync();

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryAsync(now, []);
        Assert.IsEmpty(summaries);
        Assert.IsFalse(File.Exists(Path.Combine(_temporaryDirectory!, "launch-events.jsonl")));
        Assert.IsFalse(File.Exists(Path.Combine(_temporaryDirectory!, "launch-summary.json")));
    }

    [TestMethod]
    public async Task GetSummaryAsync_AppliesPathRulesToIgnoredState()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        await store.AppendAsync(new LaunchHistoryEvent("ToolA", "x64", 100, @"C:\Apps\Tool.exe", null, now));
        await store.AppendAsync(new LaunchHistoryEvent("ToolB", "x64", 101, @"C:\Other\Tool.exe", null, now.AddMinutes(1)));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryWithRulesAsync(
            now.AddMinutes(2),
            [
                new AppIdentityRule(
                    "Ignore one path",
                    ExecutablePath: @"c:\apps\tool.exe",
                    Targets: SuppressionTarget.History)
            ]);

        Assert.IsTrue(FindSummary(summaries, "ToolA").IsIgnored);
        Assert.IsFalse(FindSummary(summaries, "ToolB").IsIgnored);
    }

    [TestMethod]
    public async Task GetSummaryAsync_KeepsSameNameDifferentPathSummariesSeparate()
    {
        LaunchHistoryStore store = CreateStore();
        DateTimeOffset now = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        await store.AppendAsync(new LaunchHistoryEvent("Tool", "x64", 100, @"C:\Apps\Tool.exe", null, now));
        await store.AppendAsync(new LaunchHistoryEvent("Tool", "x64", 101, @"C:\Other\Tool.exe", null, now.AddMinutes(1)));

        IReadOnlyList<LaunchHistorySummary> summaries = await store.GetSummaryWithRulesAsync(
            now.AddMinutes(2),
            [
                new AppIdentityRule(
                    "Ignore one path",
                    ExecutablePath: @"c:\apps\tool.exe",
                    Targets: SuppressionTarget.History)
            ]);

        Assert.HasCount(2, summaries);
        Assert.IsTrue(FindSummaryByPath(summaries, @"C:\Apps\Tool.exe").IsIgnored);
        Assert.IsFalse(FindSummaryByPath(summaries, @"C:\Other\Tool.exe").IsIgnored);
    }

    private LaunchHistoryStore CreateStore()
    {
        _temporaryDirectory ??= Path.Combine(Path.GetTempPath(), "PrismMonitorTests", Guid.NewGuid().ToString("N"));
        return new LaunchHistoryStore(
            Path.Combine(_temporaryDirectory, "launch-events.jsonl"),
            Path.Combine(_temporaryDirectory, "launch-summary.json"));
    }

    private static LaunchHistorySummary FindSummary(
        IEnumerable<LaunchHistorySummary> summaries,
        string processName)
    {
        return summaries.Single(summary => string.Equals(
            summary.ProcessName,
            processName,
            StringComparison.OrdinalIgnoreCase));
    }

    private static LaunchHistorySummary FindSummaryByPath(
        IEnumerable<LaunchHistorySummary> summaries,
        string executablePath)
    {
        return summaries.Single(summary => string.Equals(
            summary.LastExecutablePath,
            executablePath,
            StringComparison.OrdinalIgnoreCase));
    }
}
