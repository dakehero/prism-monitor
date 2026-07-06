using PrismMonitor.Core.History;
using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class LaunchHistoryRecorderTests
{
    [TestMethod]
    public void CaptureNewEvents_ReturnsEachProcessIdOnlyOnce()
    {
        LaunchHistoryRecorder recorder = new();
        DateTimeOffset detectedAt = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        CompatibilityProcessInfo process = new("Chrome", 100, "x64", TimeSpan.FromSeconds(1), @"C:\Apps\Chrome.exe");

        IReadOnlyList<LaunchHistoryEvent> first = recorder.CaptureNewEvents([process], detectedAt);
        IReadOnlyList<LaunchHistoryEvent> second = recorder.CaptureNewEvents([process], detectedAt.AddMinutes(1));

        Assert.HasCount(1, first);
        Assert.IsEmpty(second);
        Assert.AreEqual("Chrome", first[0].ProcessName);
        Assert.AreEqual("x64", first[0].Architecture);
        Assert.AreEqual(100, first[0].ProcessId);
        Assert.AreEqual(@"C:\Apps\Chrome.exe", first[0].ExecutablePath);
        Assert.IsNull(first[0].StartedAt);
        Assert.AreEqual(detectedAt, first[0].DetectedAt);
    }

    [TestMethod]
    public void CaptureNewEvents_ReturnsNewEventForSameNameWithDifferentProcessId()
    {
        LaunchHistoryRecorder recorder = new();
        DateTimeOffset detectedAt = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);

        _ = recorder.CaptureNewEvents([
            new CompatibilityProcessInfo("Chrome", 100, "x64", TimeSpan.FromSeconds(1), @"C:\Apps\Chrome.exe")
        ], detectedAt);

        IReadOnlyList<LaunchHistoryEvent> result = recorder.CaptureNewEvents([
            new CompatibilityProcessInfo("Chrome", 101, "x64", TimeSpan.FromSeconds(2), @"C:\Apps\Chrome.exe")
        ], detectedAt.AddMinutes(1));

        Assert.HasCount(1, result);
        Assert.AreEqual("Chrome", result[0].ProcessName);
        Assert.AreEqual(101, result[0].ProcessId);
        Assert.AreEqual(detectedAt.AddMinutes(1), result[0].DetectedAt);
    }
}
