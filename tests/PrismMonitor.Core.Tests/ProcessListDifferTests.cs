using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ProcessListDifferTests
{
    [TestMethod]
    public void Diff_ReturnsAddedUpdatedAndRemovedRows_ByProcessId()
    {
        int[] existingProcessIds = [10, 20, 30];
        CompatibilityProcessInfo[] snapshot =
        [
            new("updated", 10, "x64", TimeSpan.FromSeconds(5)),
            new("new", 40, "x86", TimeSpan.FromSeconds(1))
        ];

        ProcessListDiff diff = ProcessListDiffer.Diff(existingProcessIds, snapshot);

        CollectionAssert.AreEqual(new[] { 40 }, diff.Added.Select(process => process.ProcessId).ToArray());
        CollectionAssert.AreEqual(new[] { 10 }, diff.Updated.Select(process => process.ProcessId).ToArray());
        CollectionAssert.AreEqual(new[] { 20, 30 }, diff.RemovedProcessIds.ToArray());
    }

    [TestMethod]
    public void Diff_ReturnsSortedRows_ByCpuTimeNameAndProcessId()
    {
        CompatibilityProcessInfo[] snapshot =
        [
            new("beta", 30, "x64", TimeSpan.FromSeconds(5)),
            new("alpha", 20, "x64", TimeSpan.FromSeconds(5)),
            new("alpha", 10, "x64", TimeSpan.FromSeconds(5)),
            new("large", 40, "x64", TimeSpan.FromSeconds(30))
        ];

        ProcessListDiff diff = ProcessListDiffer.Diff([], snapshot);

        CollectionAssert.AreEqual(
            new[] { 40, 10, 20, 30 },
            diff.SortedRows.Select(process => process.ProcessId).ToArray());
    }

    [TestMethod]
    public void Diff_DoesNotMutateInputSnapshot()
    {
        CompatibilityProcessInfo[] snapshot =
        [
            new("small", 10, "x64", TimeSpan.FromSeconds(1)),
            new("large", 20, "x64", TimeSpan.FromSeconds(30))
        ];

        _ = ProcessListDiffer.Diff([], snapshot);

        CollectionAssert.AreEqual(new[] { 10, 20 }, snapshot.Select(process => process.ProcessId).ToArray());
    }
}
