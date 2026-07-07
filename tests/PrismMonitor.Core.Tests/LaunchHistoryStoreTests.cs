using PrismMonitor.Core.History;

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

    private LaunchHistoryStore CreateStore()
    {
        _temporaryDirectory ??= Path.Combine(Path.GetTempPath(), "PrismMonitorTests", Guid.NewGuid().ToString("N"));
        return new LaunchHistoryStore(
            Path.Combine(_temporaryDirectory, "launch-events.jsonl"),
            Path.Combine(_temporaryDirectory, "launch-summary.json"));
    }
}
