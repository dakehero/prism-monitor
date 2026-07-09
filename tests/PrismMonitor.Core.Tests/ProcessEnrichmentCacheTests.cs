using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ProcessEnrichmentCacheTests
{
    [TestMethod]
    public void GetOrAdd_ReusesMetadataForSameProcessIdentity()
    {
        ProcessEnrichmentCache cache = new();
        int factoryCalls = 0;
        ProcessSnapshotInfo snapshot = new(100, "Tool", TimeSpan.FromSeconds(1), DateTimeOffset.UnixEpoch);

        ProcessEnrichmentInfo first = cache.GetOrAdd(snapshot, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x64", @"C:\Apps\Tool.exe");
        });
        ProcessEnrichmentInfo second = cache.GetOrAdd(snapshot with { CpuTime = TimeSpan.FromSeconds(5) }, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x86", @"C:\Other\Tool.exe");
        });

        Assert.AreEqual(1, factoryCalls);
        Assert.AreEqual(first, second);
        Assert.AreEqual(@"C:\Apps\Tool.exe", second.ExecutablePath);
    }

    [TestMethod]
    public void GetOrAdd_InvalidatesMetadataWhenProcessCreationIdentityChanges()
    {
        ProcessEnrichmentCache cache = new();
        int factoryCalls = 0;
        ProcessSnapshotInfo firstSnapshot = new(100, "Tool", TimeSpan.FromSeconds(1), DateTimeOffset.UnixEpoch);
        ProcessSnapshotInfo reusedPidSnapshot = firstSnapshot with
        {
            CreationTime = DateTimeOffset.UnixEpoch.AddMinutes(1)
        };

        _ = cache.GetOrAdd(firstSnapshot, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x64", @"C:\Apps\Tool.exe");
        });
        ProcessEnrichmentInfo second = cache.GetOrAdd(reusedPidSnapshot, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x86", @"C:\Other\Tool.exe");
        });

        Assert.AreEqual(2, factoryCalls);
        Assert.AreEqual("x86", second.Architecture);
    }

    [TestMethod]
    public void GetOrAdd_DoesNotReuseMetadataWhenCreationIdentityIsMissing()
    {
        ProcessEnrichmentCache cache = new();
        int factoryCalls = 0;
        ProcessSnapshotInfo snapshot = new(100, "Tool", TimeSpan.FromSeconds(1), CreationTime: null);

        _ = cache.GetOrAdd(snapshot, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x64", @"C:\Apps\Tool.exe");
        });
        ProcessEnrichmentInfo second = cache.GetOrAdd(snapshot, _ =>
        {
            factoryCalls++;
            return new ProcessEnrichmentInfo("x86", @"C:\Other\Tool.exe");
        });

        Assert.AreEqual(2, factoryCalls);
        Assert.AreEqual("x86", second.Architecture);
    }

    [TestMethod]
    public void Prune_RemovesMetadataForExitedProcesses()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo removedSnapshot = new(100, "Removed", TimeSpan.Zero, DateTimeOffset.UnixEpoch);
        ProcessSnapshotInfo activeSnapshot = new(200, "Active", TimeSpan.Zero, DateTimeOffset.UnixEpoch);
        _ = cache.GetOrAdd(removedSnapshot, _ => new ProcessEnrichmentInfo("x64", @"C:\Removed.exe"));
        _ = cache.GetOrAdd(activeSnapshot, _ => new ProcessEnrichmentInfo("x86", @"C:\Active.exe"));

        cache.Prune([activeSnapshot]);

        Assert.IsFalse(cache.Contains(removedSnapshot));
        Assert.IsTrue(cache.Contains(activeSnapshot));
    }
}
