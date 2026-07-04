using NativeGuard.Core.Processes;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class NonNativeProcessNotifierTests
{
    [TestMethod]
    public void CaptureNewProcesses_ReturnsNoProcesses_ForInitialSnapshot()
    {
        NonNativeProcessNotifier notifier = new();

        IReadOnlyList<NonNativeProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("chrome", 10, "x64", TimeSpan.FromSeconds(1))
        ]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CaptureNewProcesses_ReturnsOnlyProcessesNotSeenInPreviousSnapshot()
    {
        NonNativeProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses(
        [
            new("existing", 10, "x64", TimeSpan.FromSeconds(1))
        ]);

        IReadOnlyList<NonNativeProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("existing", 10, "x64", TimeSpan.FromSeconds(2)),
            new("new", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        CollectionAssert.AreEqual(new[] { 20 }, result.Select(process => process.ProcessId).ToArray());
    }

    [TestMethod]
    public void CaptureNewProcesses_DoesNotRepeatSamePid()
    {
        NonNativeProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses([]);
        _ = notifier.CaptureNewProcesses(
        [
            new("new", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        IReadOnlyList<NonNativeProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("new", 20, "x86", TimeSpan.FromSeconds(2))
        ]);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CaptureNewProcesses_TreatsReappearingPidAsNewAfterItWasAbsent()
    {
        NonNativeProcessNotifier notifier = new();
        _ = notifier.CaptureNewProcesses(
        [
            new("first", 20, "x86", TimeSpan.FromSeconds(1))
        ]);
        _ = notifier.CaptureNewProcesses([]);

        IReadOnlyList<NonNativeProcessInfo> result = notifier.CaptureNewProcesses(
        [
            new("second", 20, "x86", TimeSpan.FromSeconds(1))
        ]);

        CollectionAssert.AreEqual(new[] { 20 }, result.Select(process => process.ProcessId).ToArray());
    }
}
