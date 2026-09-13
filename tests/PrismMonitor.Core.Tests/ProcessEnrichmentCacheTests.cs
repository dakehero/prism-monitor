using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ProcessEnrichmentCacheTests
{
    [TestMethod]
    public async Task GetOrEnrichAsync_ReusesCompleteResultForVerifiedInstance()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo snapshot = Snapshot(100, verified: true);
        int calls = 0;

        ProcessEnrichmentLookup first = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch,
            Factory);
        ProcessEnrichmentLookup second = await cache.GetOrEnrichAsync(
            snapshot with { CpuTime = TimeSpan.FromSeconds(5) },
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            Factory);

        Assert.AreEqual(1, calls);
        Assert.IsFalse(first.IsCacheHit);
        Assert.IsTrue(second.IsCacheHit);
        Assert.AreEqual(@"C:\Apps\Tool.exe", second.Enrichment.ExecutablePath);

        Task<ProcessEnrichmentInfo> Factory(
            ProcessSnapshotInfo _,
            ProcessEnrichmentRequest __,
            CancellationToken ___)
        {
            calls++;
            return Task.FromResult(CompatibleComplete());
        }
    }

    [TestMethod]
    public async Task GetOrEnrichAsync_InvalidatesWhenInstanceKeyChanges()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo first = Snapshot(100, verified: true);
        ProcessSnapshotInfo reused = Snapshot(
            100,
            verified: true,
            identityTime: DateTimeOffset.UnixEpoch.AddMinutes(1));
        int calls = 0;

        _ = await cache.GetOrEnrichAsync(first, ProcessEnrichmentRequest.Full, DateTimeOffset.UnixEpoch, Factory);
        _ = await cache.GetOrEnrichAsync(reused, ProcessEnrichmentRequest.Full, DateTimeOffset.UnixEpoch.AddMinutes(1), Factory);

        Assert.AreEqual(2, calls);

        Task<ProcessEnrichmentInfo> Factory(
            ProcessSnapshotInfo _,
            ProcessEnrichmentRequest __,
            CancellationToken ___)
        {
            calls++;
            return Task.FromResult(CompatibleComplete());
        }
    }

    [TestMethod]
    public async Task GetOrEnrichAsync_RevalidatesUnverifiedIdentityAfterThirtySeconds()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo snapshot = Snapshot(100, verified: false);
        int calls = 0;

        _ = await cache.GetOrEnrichAsync(snapshot, ProcessEnrichmentRequest.Full, DateTimeOffset.UnixEpoch, Factory);
        ProcessEnrichmentLookup before = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(29),
            Factory);
        ProcessEnrichmentLookup due = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(30),
            Factory);

        Assert.AreEqual(2, calls);
        Assert.IsTrue(before.IsCacheHit);
        Assert.IsFalse(due.IsCacheHit);
        Assert.IsTrue(due.IsRetry);

        Task<ProcessEnrichmentInfo> Factory(
            ProcessSnapshotInfo _,
            ProcessEnrichmentRequest __,
            CancellationToken ___)
        {
            calls++;
            return Task.FromResult(CompatibleComplete());
        }
    }

    [TestMethod]
    public async Task GetOrEnrichAsync_DoesNotRetryLimitedResultBeforeBackoffExpires()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo snapshot = Snapshot(100, verified: true);
        int calls = 0;

        ProcessEnrichmentLookup first = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch,
            Factory);
        ProcessEnrichmentLookup before = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(29),
            Factory);
        ProcessEnrichmentLookup due = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(30),
            Factory);

        Assert.IsTrue(first.Enrichment.HasLimitedDetails);
        Assert.IsTrue(before.IsCacheHit);
        Assert.IsTrue(due.IsRetry);
        Assert.AreEqual(2, calls);

        Task<ProcessEnrichmentInfo> Factory(
            ProcessSnapshotInfo _,
            ProcessEnrichmentRequest request,
            CancellationToken ___)
        {
            calls++;
            return Task.FromResult(calls == 1
                ? CompatibleLimited(request)
                : CompatibleComplete());
        }
    }

    [TestMethod]
    public async Task GetOrEnrichAsync_PreservesKnownCompatibilityWhenRetryBecomesUnknown()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo snapshot = Snapshot(100, verified: true);

        _ = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch,
            (_, _, _) => Task.FromResult(CompatibleComplete()));
        ProcessEnrichmentLookup retry = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            (_, _, _) => Task.FromResult(ProcessEnrichmentInfo.UnknownLimited("access denied")),
            forceRetry: true);

        Assert.AreEqual(ProcessCompatibilityState.Compatible, retry.Enrichment.Compatibility);
        Assert.AreEqual("x64", retry.Enrichment.Architecture);
        Assert.AreEqual(@"C:\Apps\Tool.exe", retry.Enrichment.ExecutablePath);
        Assert.IsTrue(retry.Enrichment.HasLimitedDetails);
        Assert.AreEqual("access denied", retry.Enrichment.LastError);
    }

    [TestMethod]
    public async Task GetOrEnrichAsync_ImmediatelyUpgradesHigherDetailRequest()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo snapshot = Snapshot(100, verified: true);
        List<ProcessEnrichmentRequest> requests = [];

        _ = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Classification,
            DateTimeOffset.UnixEpoch,
            Factory);
        ProcessEnrichmentLookup full = await cache.GetOrEnrichAsync(
            snapshot,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            Factory);

        Assert.HasCount(2, requests);
        Assert.AreEqual(ProcessEnrichmentLevel.Full, requests[1].Level);
        Assert.IsFalse(full.IsCacheHit);
        Assert.AreEqual(@"C:\Apps\Tool.exe", full.Enrichment.ExecutablePath);

        Task<ProcessEnrichmentInfo> Factory(
            ProcessSnapshotInfo _,
            ProcessEnrichmentRequest request,
            CancellationToken ___)
        {
            requests.Add(request);
            return Task.FromResult(request.Level == ProcessEnrichmentLevel.Full
                ? CompatibleComplete()
                : new ProcessEnrichmentInfo(
                    ProcessCompatibilityState.Compatible,
                    Architecture: "x64",
                    Level: request.Level,
                    AttemptedFields: request.IdentityFields));
        }
    }

    [TestMethod]
    public async Task Prune_RemovesExitedProcessInstances()
    {
        ProcessEnrichmentCache cache = new();
        ProcessSnapshotInfo removed = Snapshot(100, verified: true);
        ProcessSnapshotInfo active = Snapshot(200, verified: true);

        _ = await cache.GetOrEnrichAsync(
            removed,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch,
            (_, _, _) => Task.FromResult(CompatibleComplete()));
        _ = await cache.GetOrEnrichAsync(
            active,
            ProcessEnrichmentRequest.Full,
            DateTimeOffset.UnixEpoch,
            (_, _, _) => Task.FromResult(CompatibleComplete()));

        int pruned = cache.Prune([active.InstanceKey]);

        Assert.AreEqual(1, pruned);
        Assert.IsFalse(cache.Contains(removed.InstanceKey));
        Assert.IsTrue(cache.Contains(active.InstanceKey));
    }

    private static ProcessSnapshotInfo Snapshot(
        int processId,
        bool verified,
        DateTimeOffset? identityTime = null)
    {
        DateTimeOffset identity = identityTime ?? DateTimeOffset.UnixEpoch;
        return new ProcessSnapshotInfo(
            processId,
            "Tool",
            TimeSpan.FromSeconds(1),
            verified ? identity : null,
            DateTimeOffset.UnixEpoch,
            new ProcessInstanceKey(processId, identity, verified));
    }

    private static ProcessEnrichmentInfo CompatibleComplete()
    {
        return new ProcessEnrichmentInfo(
            ProcessCompatibilityState.Compatible,
            Architecture: "x64",
            ExecutablePath: @"C:\Apps\Tool.exe",
            PackageIdentity: "Contoso.Tool_1.0.0.0_x64__abc",
            PublisherIdentity: "CN=Contoso",
            IconCacheKey: @"C:\Apps\Tool.exe",
            Level: ProcessEnrichmentLevel.Full,
            AttemptedFields: ProcessIdentityFields.All);
    }

    private static ProcessEnrichmentInfo CompatibleLimited(ProcessEnrichmentRequest request)
    {
        return new ProcessEnrichmentInfo(
            ProcessCompatibilityState.Compatible,
            Architecture: "x64",
            Level: request.Level,
            AttemptedFields: request.IdentityFields,
            HasLimitedDetails: true,
            LastError: "metadata unavailable");
    }
}
