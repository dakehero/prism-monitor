using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class CompatibilityProcessNotifierTests
{
    [TestMethod]
    public void CaptureNewProcesses_ReturnsNoProcesses_ForInitialSnapshot()
    {
        CompatibilityProcessNotifier notifier = new();

        IReadOnlyList<CompatibilityProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("chrome", 10, "x64", TimeSpan.FromSeconds(1))
        ]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CaptureNewProcesses_ReturnsOnlyProcessesNotSeenInPreviousSnapshot()
    {
        CompatibilityProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses(
        [
            new("existing", 10, "x64", TimeSpan.FromSeconds(1))
        ]);

        IReadOnlyList<CompatibilityProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("existing", 10, "x64", TimeSpan.FromSeconds(2)),
            new("new", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        CollectionAssert.AreEqual(new[] { 20 }, result.Select(process => process.ProcessId).ToArray());
    }

    [TestMethod]
    public void CaptureNewProcesses_DoesNotRepeatSamePid()
    {
        CompatibilityProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses([]);
        _ = notifier.CaptureNewProcesses(
        [
            new("new", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        IReadOnlyList<CompatibilityProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("new", 20, "x86", TimeSpan.FromSeconds(2))
        ]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CaptureNewProcesses_TreatsReappearingPidAsNewAfterItWasAbsent()
    {
        CompatibilityProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses(
        [
            new("first", 20, "x86", TimeSpan.FromSeconds(1))
        ]);
        _ = notifier.CaptureNewProcesses([]);

        IReadOnlyList<CompatibilityProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("second", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        CollectionAssert.AreEqual(new[] { 20 }, result.Select(process => process.ProcessId).ToArray());
    }

    [TestMethod]
    public void CaptureNewProcesses_TreatsReusedPidWithNewInstanceKeyAsNew()
    {
        CompatibilityProcessNotifier notifier = new();
        DateTimeOffset firstCreation = DateTimeOffset.UnixEpoch;
        DateTimeOffset reusedCreation = firstCreation.AddMinutes(1);
        _ = notifier.CaptureNewProcesses(
        [
            Process(20, firstCreation)
        ]);

        IReadOnlyList<CompatibilityProcessInfo> result = notifier.CaptureNewProcesses(
        [
            Process(20, reusedCreation)
        ]);

        Assert.HasCount(1, result);
        Assert.AreEqual(reusedCreation, result[0].InstanceKey!.Value.IdentityTime);
    }

    private static CompatibilityProcessInfo Process(int processId, DateTimeOffset creationTime)
    {
        return new CompatibilityProcessInfo(
            "tool",
            processId,
            "x64",
            TimeSpan.Zero,
            CreationTime: creationTime,
            DetectedAt: creationTime.AddSeconds(1),
            InstanceKey: new ProcessInstanceKey(processId, creationTime, IsCreationTimeVerified: true));
    }
}
