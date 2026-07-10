using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Monitoring;

public sealed class MonitoringCoordinator
{
    private static readonly CancellationToken StoppedToken = new(canceled: true);
    private readonly IProcessSnapshotProvider _snapshotProvider;
    private readonly IProcessEnricher _processEnricher;
    private readonly ProcessSnapshotTracker _snapshotTracker;
    private readonly ProcessEnrichmentCache _enrichmentCache;
    private readonly Func<DateTimeOffset> _clock;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _sync = new();
    private MonitoringConfiguration _configuration = MonitoringConfiguration.Default;
    private MonitoringSnapshot? _latestSnapshot;
    private MonitoringCycleDiagnostics _lastDiagnostics = MonitoringCycleDiagnostics.Empty;
    private MonitoringRefreshRequest? _activeRequest;
    private MonitoringRefreshRequest? _pendingRequest;
    private Task<MonitoringSnapshot>? _runTask;
    private long _sequence;
    private long _totalProviderCallCount;
    private long _totalCacheHits;
    private long _totalCacheMisses;
    private long _totalCacheRetries;
    private long _totalCachePrunes;
    private bool _isStopped;

    public MonitoringCoordinator(
        IProcessSnapshotProvider snapshotProvider,
        IProcessEnricher processEnricher,
        ProcessSnapshotTracker? snapshotTracker = null,
        ProcessEnrichmentCache? enrichmentCache = null,
        Func<DateTimeOffset>? clock = null)
    {
        _snapshotProvider = snapshotProvider;
        _processEnricher = processEnricher;
        _snapshotTracker = snapshotTracker ?? new ProcessSnapshotTracker();
        _enrichmentCache = enrichmentCache ?? new ProcessEnrichmentCache();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public event EventHandler<MonitoringSnapshot>? SnapshotPublished;

    public MonitoringSnapshot? LatestSnapshot
    {
        get
        {
            lock (_sync)
            {
                return _latestSnapshot;
            }
        }
    }

    public MonitoringCycleDiagnostics LastDiagnostics
    {
        get
        {
            lock (_sync)
            {
                return _lastDiagnostics;
            }
        }
    }

    public void UpdateConfiguration(MonitoringConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        lock (_sync)
        {
            _configuration = new MonitoringConfiguration(
                configuration.Rules.ToArray(),
                configuration.Settings);
        }
    }

    public Task<MonitoringSnapshot> RequestRefreshAsync(
        MonitoringRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Task<MonitoringSnapshot> sharedTask;
        lock (_sync)
        {
            if (_isStopped)
            {
                return Task.FromCanceled<MonitoringSnapshot>(StoppedToken);
            }

            if (_runTask is null)
            {
                _pendingRequest = request;
                _runTask = Task.Run(RunLoopAsync);
            }
            else if (_activeRequest is null)
            {
                _pendingRequest = Merge(_pendingRequest, request);
            }
            else if (NeedsFollowUp(_activeRequest, request))
            {
                _pendingRequest = Merge(_pendingRequest, request);
            }

            sharedTask = _runTask;
        }

        return cancellationToken.CanBeCanceled
            ? sharedTask.WaitAsync(cancellationToken)
            : sharedTask;
    }

    public async Task StopAsync()
    {
        Task<MonitoringSnapshot>? runTask;
        bool shouldCancel;
        lock (_sync)
        {
            shouldCancel = !_isStopped;
            _isStopped = true;
            runTask = _runTask;
        }

        if (shouldCancel)
        {
            _shutdown.Cancel();
        }

        if (runTask is null)
        {
            return;
        }

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task<MonitoringSnapshot> RunLoopAsync()
    {
        try
        {
            MonitoringSnapshot result;
            while (true)
            {
                MonitoringRefreshRequest request;
                lock (_sync)
                {
                    request = _pendingRequest ?? MonitoringRefreshRequest.Periodic;
                    _pendingRequest = null;
                    _activeRequest = request;
                }

                result = await RunCycleAsync(request).ConfigureAwait(false);

                lock (_sync)
                {
                    _activeRequest = null;
                    if (_pendingRequest is null)
                    {
                        _runTask = null;
                        return result;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            lock (_sync)
            {
                _activeRequest = null;
                _pendingRequest = null;
                _runTask = null;
            }

            throw;
        }
    }

    private async Task<MonitoringSnapshot> RunCycleAsync(MonitoringRefreshRequest refreshRequest)
    {
        DateTimeOffset startedAt = _clock();
        MonitoringConfiguration configuration = GetConfiguration();
        IReadOnlyList<ProcessObservation> observations;
        long totalProviderCallCount = Interlocked.Increment(ref _totalProviderCallCount);
        try
        {
            observations = await _snapshotProvider.CaptureAsync(_shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DateTimeOffset failedAt = _clock();
            MonitoringCycleDiagnostics diagnostics = new(
                Succeeded: false,
                refreshRequest.Reason,
                startedAt,
                failedAt,
                ProviderCallCount: 1,
                ObservedProcessCount: 0,
                CompatibleProcessCount: 0,
                CacheHits: 0,
                CacheMisses: 0,
                CacheRetries: 0,
                CachePrunes: 0,
                exception.Message)
            {
                TotalProviderCallCount = totalProviderCallCount,
                TotalCacheHits = Interlocked.Read(ref _totalCacheHits),
                TotalCacheMisses = Interlocked.Read(ref _totalCacheMisses),
                TotalCacheRetries = Interlocked.Read(ref _totalCacheRetries),
                TotalCachePrunes = Interlocked.Read(ref _totalCachePrunes)
            };

            lock (_sync)
            {
                _lastDiagnostics = diagnostics;
                return _latestSnapshot ?? MonitoringSnapshotBuilder.Build(
                    [],
                    configuration.Rules,
                    configuration.Settings,
                    capturedAt: failedAt);
            }
        }

        IReadOnlyList<ProcessSnapshotInfo> snapshots = _snapshotTracker.Track(observations, startedAt);
        int pruned = _enrichmentCache.Prune(snapshots.Select(snapshot => snapshot.InstanceKey));
        ProcessEnrichmentRequest enrichmentRequest = CreateEnrichmentRequest(
            configuration.Rules,
            refreshRequest.RequestFullDetails);
        List<CompatibilityProcessInfo> compatibleProcesses = [];
        int cacheHits = 0;
        int cacheMisses = 0;
        int cacheRetries = 0;

        foreach (ProcessSnapshotInfo snapshot in snapshots)
        {
            ProcessEnrichmentLookup lookup = await _enrichmentCache.GetOrEnrichAsync(
                    snapshot,
                    enrichmentRequest,
                    startedAt,
                    _processEnricher.EnrichAsync,
                    cancellationToken: _shutdown.Token)
                .ConfigureAwait(false);

            if (lookup.IsCacheHit)
            {
                cacheHits++;
            }
            else if (lookup.IsRetry)
            {
                cacheRetries++;
            }
            else
            {
                cacheMisses++;
            }

            ProcessEnrichmentInfo enrichment = lookup.Enrichment;
            if (enrichment.Compatibility != ProcessCompatibilityState.Compatible)
            {
                continue;
            }

            bool hasLimitedDetails = enrichment.HasLimitedDetails
                || string.IsNullOrWhiteSpace(enrichment.Architecture);
            compatibleProcesses.Add(new CompatibilityProcessInfo(
                snapshot.Name,
                snapshot.ProcessId,
                enrichment.Architecture ?? "Unknown",
                snapshot.CpuTime,
                enrichment.ExecutablePath,
                enrichment.PackageIdentity,
                enrichment.PublisherIdentity,
                snapshot.CreationTime,
                snapshot.DetectedAt,
                snapshot.InstanceKey,
                enrichment.IconCacheKey,
                hasLimitedDetails));
        }

        IReadOnlyList<CompatibilityProcessInfo> sortedProcesses = compatibleProcesses
            .OrderByDescending(process => process.CpuTime ?? TimeSpan.MinValue)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.ProcessId)
            .ToList();
        DateTimeOffset capturedAt = _clock();
        long sequence = Interlocked.Increment(ref _sequence);
        MonitoringSnapshot monitoringSnapshot = MonitoringSnapshotBuilder.Build(
            sortedProcesses,
            configuration.Rules,
            configuration.Settings,
            sequence,
            capturedAt);
        long totalCacheHits = Interlocked.Add(ref _totalCacheHits, cacheHits);
        long totalCacheMisses = Interlocked.Add(ref _totalCacheMisses, cacheMisses);
        long totalCacheRetries = Interlocked.Add(ref _totalCacheRetries, cacheRetries);
        long totalCachePrunes = Interlocked.Add(ref _totalCachePrunes, pruned);
        MonitoringCycleDiagnostics successfulDiagnostics = new(
            Succeeded: true,
            refreshRequest.Reason,
            startedAt,
            capturedAt,
            ProviderCallCount: 1,
            ObservedProcessCount: snapshots.Count,
            CompatibleProcessCount: compatibleProcesses.Count,
            cacheHits,
            cacheMisses,
            cacheRetries,
            pruned)
        {
            TotalProviderCallCount = totalProviderCallCount,
            TotalCacheHits = totalCacheHits,
            TotalCacheMisses = totalCacheMisses,
            TotalCacheRetries = totalCacheRetries,
            TotalCachePrunes = totalCachePrunes
        };

        lock (_sync)
        {
            _latestSnapshot = monitoringSnapshot;
            _lastDiagnostics = successfulDiagnostics;
        }

        PublishSnapshot(monitoringSnapshot);
        return monitoringSnapshot;
    }

    private MonitoringConfiguration GetConfiguration()
    {
        lock (_sync)
        {
            return _configuration;
        }
    }

    private void PublishSnapshot(MonitoringSnapshot snapshot)
    {
        Delegate[] handlers = SnapshotPublished?.GetInvocationList() ?? [];
        foreach (EventHandler<MonitoringSnapshot> handler in handlers.Cast<EventHandler<MonitoringSnapshot>>())
        {
            try
            {
                handler(this, snapshot);
            }
            catch
            {
                // A consumer cannot invalidate a successfully captured snapshot.
            }
        }
    }

    private static ProcessEnrichmentRequest CreateEnrichmentRequest(
        IReadOnlyList<AppIdentityRule> rules,
        bool requestFullDetails)
    {
        if (requestFullDetails)
        {
            return ProcessEnrichmentRequest.Full;
        }

        ProcessIdentityFields fields = ProcessIdentityFields.None;
        foreach (AppIdentityRule rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.ExecutablePath))
            {
                fields |= ProcessIdentityFields.ExecutablePath;
            }

            if (!string.IsNullOrWhiteSpace(rule.PackageIdentity))
            {
                fields |= ProcessIdentityFields.PackageIdentity;
            }

            if (!string.IsNullOrWhiteSpace(rule.PublisherIdentity))
            {
                fields |= ProcessIdentityFields.PublisherIdentity;
            }
        }

        return fields == ProcessIdentityFields.None
            ? ProcessEnrichmentRequest.Classification
            : new ProcessEnrichmentRequest(ProcessEnrichmentLevel.RuleIdentity, fields);
    }

    private static bool NeedsFollowUp(
        MonitoringRefreshRequest active,
        MonitoringRefreshRequest incoming)
    {
        return incoming.Reason == MonitoringRefreshReason.ConfigurationChanged
            || incoming.RequestFullDetails && !active.RequestFullDetails;
    }

    private static MonitoringRefreshRequest Merge(
        MonitoringRefreshRequest? current,
        MonitoringRefreshRequest incoming)
    {
        if (current is null)
        {
            return incoming;
        }

        return new MonitoringRefreshRequest(
            incoming.Reason,
            current.RequestFullDetails || incoming.RequestFullDetails);
    }
}
