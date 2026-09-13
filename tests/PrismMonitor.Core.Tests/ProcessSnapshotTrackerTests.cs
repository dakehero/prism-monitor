using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ProcessSnapshotTrackerTests
{
    [TestMethod]
    public void Track_PreservesDetectedAtForSameCreationIdentity()
    {
        ProcessSnapshotTracker tracker = new();
        DateTimeOffset creation = DateTimeOffset.UnixEpoch;
        DateTimeOffset firstDetected = creation.AddMinutes(1);

        ProcessSnapshotInfo first = tracker.Track(
            [new ProcessObservation(42, "Tool", TimeSpan.FromSeconds(1), creation)],
            firstDetected).Single();
        ProcessSnapshotInfo second = tracker.Track(
            [new ProcessObservation(42, "Tool", TimeSpan.FromSeconds(2), creation)],
            firstDetected.AddSeconds(3)).Single();

        Assert.AreEqual(firstDetected, second.DetectedAt);
        Assert.AreEqual(first.InstanceKey, second.InstanceKey);
        Assert.AreEqual(TimeSpan.FromSeconds(2), second.CpuTime);
    }

    [TestMethod]
    public void Track_ReplacesIdentityWhenPidCreationTimeChanges()
    {
        ProcessSnapshotTracker tracker = new();
        DateTimeOffset detectedAt = DateTimeOffset.UnixEpoch.AddMinutes(1);
        ProcessSnapshotInfo first = tracker.Track(
            [new ProcessObservation(42, "Tool", null, DateTimeOffset.UnixEpoch)],
            detectedAt).Single();
        ProcessSnapshotInfo reused = tracker.Track(
            [new ProcessObservation(42, "Tool", null, DateTimeOffset.UnixEpoch.AddMinutes(2))],
            detectedAt.AddMinutes(2)).Single();

        Assert.AreNotEqual(first.InstanceKey, reused.InstanceKey);
        Assert.AreEqual(detectedAt.AddMinutes(2), reused.DetectedAt);
    }

    [TestMethod]
    public void Track_ReplacesFallbackIdentityAfterPidDisappears()
    {
        ProcessSnapshotTracker tracker = new();
        DateTimeOffset firstDetected = DateTimeOffset.UnixEpoch.AddMinutes(1);
        ProcessSnapshotInfo first = tracker.Track(
            [new ProcessObservation(42, "Tool", null, null)],
            firstDetected).Single();

        Assert.IsEmpty(tracker.Track([], firstDetected.AddSeconds(1)));
        ProcessSnapshotInfo reused = tracker.Track(
            [new ProcessObservation(42, "Tool", null, null)],
            firstDetected.AddSeconds(2)).Single();

        Assert.AreNotEqual(first.InstanceKey, reused.InstanceKey);
        Assert.IsFalse(reused.InstanceKey.IsCreationTimeVerified);
    }

    [TestMethod]
    public void Track_ReplacesFallbackIdentityWhenProcessNameChanges()
    {
        ProcessSnapshotTracker tracker = new();
        DateTimeOffset firstDetected = DateTimeOffset.UnixEpoch.AddMinutes(1);
        ProcessSnapshotInfo first = tracker.Track(
            [new ProcessObservation(42, "First", null, null)],
            firstDetected).Single();
        ProcessSnapshotInfo reused = tracker.Track(
            [new ProcessObservation(42, "Second", null, null)],
            firstDetected.AddSeconds(1)).Single();

        Assert.AreNotEqual(first.InstanceKey, reused.InstanceKey);
    }
}
