using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class MonitoringCoordinatorTests
{
    [TestMethod]
    public async Task RequestRefreshAsync_FansOutOneCaptureToEverySurface()
    {
        StaticSnapshotProvider provider = new([Observation(1)]);
        RecordingEnricher enricher = new(Compatible);
        MonitoringCoordinator coordinator = CreateCoordinator(provider, enricher);
        MonitoringSnapshot? published = null;
        coordinator.SnapshotPublished += (_, snapshot) => published = snapshot;

        MonitoringSnapshot result = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);

        Assert.AreEqual(1, provider.CallCount);
        Assert.AreSame(result, published);
        Assert.HasCount(1, result.Processes);
        Assert.HasCount(1, result.TrayProcesses);
        Assert.HasCount(1, result.NotifiableProcesses);
        Assert.HasCount(1, result.HistoryProcesses);
        Assert.AreEqual(1L, result.Sequence);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_CoalescesEquivalentConcurrentRequests()
    {
        ControlledSnapshotProvider provider = new([Observation(1)]);
        MonitoringCoordinator coordinator = CreateCoordinator(provider, new RecordingEnricher(Compatible));

        Task<MonitoringSnapshot> first = coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        await provider.WaitForCallAsync(1);
        Task<MonitoringSnapshot> second = coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        provider.ReleaseOne();
        MonitoringSnapshot[] results = await Task.WhenAll(first, second);

        Assert.AreEqual(1, provider.CallCount);
        Assert.AreSame(results[0], results[1]);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_RunsOnePendingFullDetailUpgrade()
    {
        ControlledSnapshotProvider provider = new([Observation(1)]);
        RecordingEnricher enricher = new(Compatible);
        MonitoringCoordinator coordinator = CreateCoordinator(provider, enricher);

        Task<MonitoringSnapshot> periodic = coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        await provider.WaitForCallAsync(1);
        Task<MonitoringSnapshot> visible = coordinator.RequestRefreshAsync(
            new MonitoringRefreshRequest(MonitoringRefreshReason.WindowVisible, RequestFullDetails: true));
        provider.ReleaseOne();
        await provider.WaitForCallAsync(2);
        provider.ReleaseOne();
        await Task.WhenAll(periodic, visible);

        Assert.AreEqual(2, provider.CallCount);
        Assert.AreEqual(ProcessEnrichmentLevel.Full, enricher.Requests.Last().Level);
        Assert.AreEqual(2L, coordinator.LatestSnapshot!.Sequence);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_KeepsLastGoodSnapshotWhenCaptureFails()
    {
        SequenceSnapshotProvider provider = new(
            [Observation(1)],
            new IOException("capture failed"));
        MonitoringCoordinator coordinator = CreateCoordinator(provider, new RecordingEnricher(Compatible));
        int publishedCount = 0;
        coordinator.SnapshotPublished += (_, _) => publishedCount++;

        MonitoringSnapshot first = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        MonitoringSnapshot second = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, publishedCount);
        Assert.IsFalse(coordinator.LastDiagnostics.Succeeded);
        Assert.AreEqual("capture failed", coordinator.LastDiagnostics.Error);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_RequestsOnlyIdentityFieldsUsedByRules()
    {
        StaticSnapshotProvider provider = new([Observation(1)]);
        RecordingEnricher enricher = new(Compatible);
        MonitoringCoordinator coordinator = CreateCoordinator(provider, enricher);
        coordinator.UpdateConfiguration(new MonitoringConfiguration(
            [new AppIdentityRule("Package rule", PackageIdentity: "Contoso.Package")],
            MonitoringSettings.Default));

        _ = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);

        ProcessEnrichmentRequest request = enricher.Requests.Single();
        Assert.AreEqual(ProcessEnrichmentLevel.RuleIdentity, request.Level);
        Assert.AreEqual(ProcessIdentityFields.PackageIdentity, request.IdentityFields);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_PreservesDetectionTimeAcrossPublishedSequences()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddHours(1);
        StaticSnapshotProvider provider = new([Observation(1)]);
        MonitoringCoordinator coordinator = CreateCoordinator(
            provider,
            new RecordingEnricher(Compatible),
            () => now);

        MonitoringSnapshot first = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        now = now.AddSeconds(3);
        MonitoringSnapshot second = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);

        Assert.AreEqual(first.Processes[0].DetectedAt, second.Processes[0].DetectedAt);
        Assert.AreEqual(1L, first.Sequence);
        Assert.AreEqual(2L, second.Sequence);
        Assert.AreEqual(now, second.CapturedAt);
        Assert.AreEqual(2L, coordinator.LastDiagnostics.TotalProviderCallCount);
        Assert.AreEqual(1L, coordinator.LastDiagnostics.TotalCacheHits);
        Assert.AreEqual(1L, coordinator.LastDiagnostics.TotalCacheMisses);
    }

    [TestMethod]
    public async Task RequestRefreshAsync_DoesNotPublishNativeOrNeverClassifiedProcesses()
    {
        StaticSnapshotProvider provider = new([Observation(1), Observation(2), Observation(3)]);
        RecordingEnricher enricher = new((snapshot, request) => snapshot.ProcessId switch
        {
            1 => Compatible(snapshot, request),
            2 => ProcessEnrichmentInfo.Native,
            _ => ProcessEnrichmentInfo.UnknownLimited("access denied")
        });
        MonitoringCoordinator coordinator = CreateCoordinator(provider, enricher);

        MonitoringSnapshot snapshot = await coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);

        CollectionAssert.AreEqual(new[] { 1 }, snapshot.Processes.Select(item => item.ProcessId).ToArray());
        Assert.AreEqual(3, coordinator.LastDiagnostics.ObservedProcessCount);
        Assert.AreEqual(1, coordinator.LastDiagnostics.CompatibleProcessCount);
    }

    [TestMethod]
    public async Task StopAsync_CancelsAndWaitsForActiveCapture()
    {
        CancellationAwareSnapshotProvider provider = new();
        MonitoringCoordinator coordinator = CreateCoordinator(
            provider,
            new RecordingEnricher(Compatible));

        Task<MonitoringSnapshot> refresh = coordinator.RequestRefreshAsync(MonitoringRefreshRequest.Periodic);
        await provider.Started;

        await coordinator.StopAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await refresh);
        Assert.IsTrue(provider.CancellationObserved);
    }

    private static MonitoringCoordinator CreateCoordinator(
        IProcessSnapshotProvider provider,
        IProcessEnricher enricher,
        Func<DateTimeOffset>? clock = null)
    {
        MonitoringCoordinator coordinator = new(provider, enricher, clock: clock);
        coordinator.UpdateConfiguration(MonitoringConfiguration.Default);
        return coordinator;
    }

    private static ProcessObservation Observation(int processId)
    {
        return new ProcessObservation(
            processId,
            $"Tool{processId}",
            TimeSpan.FromSeconds(processId),
            DateTimeOffset.UnixEpoch.AddMinutes(processId));
    }

    private static ProcessEnrichmentInfo Compatible(
        ProcessSnapshotInfo snapshot,
        ProcessEnrichmentRequest request)
    {
        return new ProcessEnrichmentInfo(
            ProcessCompatibilityState.Compatible,
            Architecture: "x64",
            ExecutablePath: request.IdentityFields.HasFlag(ProcessIdentityFields.ExecutablePath)
                ? $@"C:\Apps\{snapshot.Name}.exe"
                : null,
            PackageIdentity: request.IdentityFields.HasFlag(ProcessIdentityFields.PackageIdentity)
                ? "Contoso.Package"
                : null,
            PublisherIdentity: request.IdentityFields.HasFlag(ProcessIdentityFields.PublisherIdentity)
                ? "CN=Contoso"
                : null,
            Level: request.Level,
            AttemptedFields: request.IdentityFields);
    }

    private sealed class StaticSnapshotProvider(IReadOnlyList<ProcessObservation> observations)
        : IProcessSnapshotProvider
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<ProcessObservation>> CaptureAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(observations);
        }
    }

    private sealed class SequenceSnapshotProvider(
        IReadOnlyList<ProcessObservation> first,
        Exception second)
        : IProcessSnapshotProvider
    {
        private int _callCount;

        public Task<IReadOnlyList<ProcessObservation>> CaptureAsync(CancellationToken cancellationToken = default)
        {
            _callCount++;
            return _callCount == 1
                ? Task.FromResult(first)
                : Task.FromException<IReadOnlyList<ProcessObservation>>(second);
        }
    }

    private sealed class ControlledSnapshotProvider(IReadOnlyList<ProcessObservation> observations)
        : IProcessSnapshotProvider
    {
        private readonly TaskCompletionSource[] _started = Enumerable
            .Range(0, 4)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        private readonly SemaphoreSlim _releases = new(0, 4);
        private int _callCount;

        public int CallCount => _callCount;

        public async Task<IReadOnlyList<ProcessObservation>> CaptureAsync(
            CancellationToken cancellationToken = default)
        {
            int call = Interlocked.Increment(ref _callCount);
            _started[call - 1].TrySetResult();
            await _releases.WaitAsync(cancellationToken);
            return observations;
        }

        public Task WaitForCallAsync(int callNumber)
        {
            return _started[callNumber - 1].Task;
        }

        public void ReleaseOne()
        {
            _releases.Release();
        }
    }

    private sealed class RecordingEnricher(
        Func<ProcessSnapshotInfo, ProcessEnrichmentRequest, ProcessEnrichmentInfo> handler)
        : IProcessEnricher
    {
        public List<ProcessEnrichmentRequest> Requests { get; } = [];

        public Task<ProcessEnrichmentInfo> EnrichAsync(
            ProcessSnapshotInfo snapshot,
            ProcessEnrichmentRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(handler(snapshot, request));
        }
    }

    private sealed class CancellationAwareSnapshotProvider : IProcessSnapshotProvider
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public bool CancellationObserved { get; private set; }

        public async Task<IReadOnlyList<ProcessObservation>> CaptureAsync(
            CancellationToken cancellationToken = default)
        {
            _started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return [];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }
}
