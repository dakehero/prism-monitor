using PrismMonitor.Core.History;
using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class LaunchHistoryRecorderTests
{
    [TestMethod]
    public void CaptureNewEvents_RecordsProcessInstanceOnlyOnce()
    {
        LaunchHistoryRecorder recorder = new();
        DateTimeOffset creationTime = new(2026, 7, 6, 7, 59, 55, TimeSpan.Zero);
        DateTimeOffset detectedAt = new(2026, 7, 6, 8, 0, 0, TimeSpan.Zero);
        CompatibilityProcessInfo process = Process(100, creationTime, detectedAt);

        IReadOnlyList<LaunchHistoryEvent> first = recorder.CaptureNewEvents([process]);
        IReadOnlyList<LaunchHistoryEvent> second = recorder.CaptureNewEvents([process]);

        Assert.HasCount(1, first);
        Assert.IsEmpty(second);
        Assert.AreEqual(process.InstanceKey, first[0].InstanceKey);
        Assert.AreEqual(creationTime, first[0].StartedAt);
        Assert.AreEqual(detectedAt, first[0].DetectedAt);
    }

    [TestMethod]
    public void CaptureNewEvents_RecordsReusedPidWithNewInstanceKey()
    {
        LaunchHistoryRecorder recorder = new();
        DateTimeOffset firstCreation = DateTimeOffset.UnixEpoch;
        DateTimeOffset reusedCreation = firstCreation.AddMinutes(1);

        _ = recorder.CaptureNewEvents([Process(42, firstCreation, firstCreation.AddSeconds(1))]);
        IReadOnlyList<LaunchHistoryEvent> reused = recorder.CaptureNewEvents(
            [Process(42, reusedCreation, reusedCreation.AddSeconds(1))]);

        Assert.HasCount(1, reused);
        Assert.AreEqual(reusedCreation, reused[0].StartedAt);
        Assert.AreEqual(reusedCreation, reused[0].InstanceKey!.Value.IdentityTime);
    }

    [TestMethod]
    public void CaptureNewEvents_ForgetsInstanceAfterItLeavesCurrentSnapshot()
    {
        LaunchHistoryRecorder recorder = new();
        DateTimeOffset creation = DateTimeOffset.UnixEpoch;
        CompatibilityProcessInfo process = Process(42, creation, creation.AddSeconds(1));

        _ = recorder.CaptureNewEvents([process]);
        Assert.IsEmpty(recorder.CaptureNewEvents([]));
        IReadOnlyList<LaunchHistoryEvent> reappeared = recorder.CaptureNewEvents([process]);

        Assert.HasCount(1, reappeared);
    }

    private static CompatibilityProcessInfo Process(
        int processId,
        DateTimeOffset creationTime,
        DateTimeOffset detectedAt)
    {
        ProcessInstanceKey key = new(processId, creationTime, IsCreationTimeVerified: true);
        return new CompatibilityProcessInfo(
            "Chrome",
            processId,
            "x64",
            TimeSpan.FromSeconds(1),
            @"C:\Apps\Chrome.exe",
            CreationTime: creationTime,
            DetectedAt: detectedAt,
            InstanceKey: key);
    }
}
