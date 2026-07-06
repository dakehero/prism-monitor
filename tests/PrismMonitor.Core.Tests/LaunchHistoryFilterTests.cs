using PrismMonitor.Core.History;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class LaunchHistoryFilterTests
{
    [TestMethod]
    public void Apply_FiltersByTextAgainstProcessNameOrExecutablePath()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(Summaries(), new LaunchHistoryQuery(Text: "TOOLS"));

        CollectionAssert.AreEqual(new[] { "Bravo" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_FiltersByTextAgainstProcessName()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(Summaries(), new LaunchHistoryQuery(Text: "alp"));

        CollectionAssert.AreEqual(new[] { "Alpha" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_FiltersByArchitecture()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(Summaries(), new LaunchHistoryQuery(Architecture: "arm64"));

        CollectionAssert.AreEqual(new[] { "Alpha" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_FiltersByArchitectureCaseInsensitive()
    {
        IReadOnlyList<LaunchHistorySummary> summaries =
        [
            Summary("NativeArm", "arm64ec", new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero)),
            Summary("Other", "x64", new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero))
        ];

        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(
            summaries,
            new LaunchHistoryQuery(Architecture: "ARM64EC"));

        CollectionAssert.AreEqual(new[] { "NativeArm" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_FiltersIgnoredOnly()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(
            Summaries(),
            new LaunchHistoryQuery(IgnoredState: LaunchHistoryIgnoredState.IgnoredOnly));

        CollectionAssert.AreEqual(new[] { "Bravo" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_FiltersNotIgnoredOnly()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(
            Summaries(),
            new LaunchHistoryQuery(IgnoredState: LaunchHistoryIgnoredState.NotIgnoredOnly));

        CollectionAssert.AreEqual(new[] { "Alpha", "Charlie" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_KeepsIgnoredAndNotIgnoredWhenIgnoredStateIsAll()
    {
        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(
            Summaries(),
            new LaunchHistoryQuery(IgnoredState: LaunchHistoryIgnoredState.All));

        CollectionAssert.AreEqual(new[] { "Alpha", "Bravo", "Charlie" }, result.Select(summary => summary.ProcessName).ToArray());
        Assert.IsTrue(result.Single(summary => summary.ProcessName == "Bravo").IsIgnored);
        Assert.IsTrue(result.Where(summary => summary.ProcessName != "Bravo").All(summary => !summary.IsIgnored));
    }

    [TestMethod]
    public void Apply_SortsByLastSeenDescendingThenProcessNameAscending()
    {
        IReadOnlyList<LaunchHistorySummary> summaries =
        [
            Summary("zulu", "x64", new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero)),
            Summary("Alpha", "x64", new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero)),
            Summary("bravo", "x64", new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero))
        ];

        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(summaries, new LaunchHistoryQuery());

        CollectionAssert.AreEqual(new[] { "Alpha", "bravo", "zulu" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    [TestMethod]
    public void Apply_SortsCaseTiesDeterministically()
    {
        DateTimeOffset lastSeenAt = new(2026, 7, 6, 9, 0, 0, TimeSpan.Zero);
        IReadOnlyList<LaunchHistorySummary> summaries =
        [
            Summary("app", "x64", lastSeenAt),
            Summary("App", "x64", lastSeenAt)
        ];

        IReadOnlyList<LaunchHistorySummary> result = LaunchHistoryFilter.Apply(summaries, new LaunchHistoryQuery());

        CollectionAssert.AreEqual(new[] { "App", "app" }, result.Select(summary => summary.ProcessName).ToArray());
    }

    private static IReadOnlyList<LaunchHistorySummary> Summaries() =>
    [
        Summary("Alpha", "arm64", new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.Zero), @"C:\Apps\Alpha.exe"),
        Summary("Bravo", "x64", new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero), @"D:\Tools\Bravo.exe", isIgnored: true),
        Summary("Charlie", "x86", new DateTimeOffset(2026, 7, 6, 7, 0, 0, TimeSpan.Zero), null)
    ];

    private static LaunchHistorySummary Summary(
        string processName,
        string architecture,
        DateTimeOffset lastSeenAt,
        string? lastExecutablePath = null,
        bool isIgnored = false) =>
        new(
            processName,
            architecture,
            LaunchCount: 1,
            FirstSeenAt: lastSeenAt.AddMinutes(-1),
            LastSeenAt: lastSeenAt,
            LastExecutablePath: lastExecutablePath)
        {
            IsIgnored = isIgnored
        };
}
