namespace PrismMonitor.Core.Processes;

public sealed record ProcessEnrichmentLookup(
    ProcessEnrichmentInfo Enrichment,
    bool IsCacheHit,
    bool IsRetry);

public sealed class ProcessEnrichmentCache
{
    private static readonly TimeSpan UnverifiedIdentityLifetime = TimeSpan.FromSeconds(30);
    private readonly Dictionary<ProcessInstanceKey, CacheEntry> _entries = [];

    public async Task<ProcessEnrichmentLookup> GetOrEnrichAsync(
        ProcessSnapshotInfo snapshot,
        ProcessEnrichmentRequest request,
        DateTimeOffset now,
        Func<ProcessSnapshotInfo, ProcessEnrichmentRequest, CancellationToken, Task<ProcessEnrichmentInfo>> factory,
        bool forceRetry = false,
        CancellationToken cancellationToken = default)
    {
        _entries.TryGetValue(snapshot.InstanceKey, out CacheEntry? entry);

        bool needsUpgrade = entry is not null && NeedsUpgrade(entry.Enrichment, request);
        bool retryDue = entry is not null
            && entry.Enrichment.HasLimitedDetails
            && entry.NextRetryAt is not null
            && now >= entry.NextRetryAt;
        bool identityRevalidationDue = entry is not null
            && !snapshot.InstanceKey.IsCreationTimeVerified
            && now - entry.LastAttemptAt >= UnverifiedIdentityLifetime;

        if (!forceRetry
            && entry is not null
            && !needsUpgrade
            && !retryDue
            && !identityRevalidationDue)
        {
            return new ProcessEnrichmentLookup(entry.Enrichment, IsCacheHit: true, IsRetry: false);
        }

        ProcessEnrichmentInfo enrichment;
        try
        {
            enrichment = await factory(snapshot, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            enrichment = ProcessEnrichmentInfo.UnknownLimited(exception.Message);
        }

        enrichment = Normalize(enrichment, request);
        if (entry is not null)
        {
            enrichment = Merge(entry.Enrichment, enrichment);
        }

        bool failed = enrichment.HasLimitedDetails
            || enrichment.Compatibility == ProcessCompatibilityState.Unknown;
        int failureCount = failed ? (entry?.FailureCount ?? 0) + 1 : 0;
        DateTimeOffset? nextRetryAt = failed
            ? now + EnrichmentRetryPolicy.GetDelay(failureCount)
            : null;

        _entries[snapshot.InstanceKey] = new CacheEntry(
            enrichment,
            now,
            nextRetryAt,
            failureCount);

        bool isRetry = entry is not null
            && (forceRetry || retryDue || identityRevalidationDue);
        return new ProcessEnrichmentLookup(enrichment, IsCacheHit: false, isRetry);
    }

    public int Prune(IEnumerable<ProcessInstanceKey> activeInstanceKeys)
    {
        HashSet<ProcessInstanceKey> activeKeys = activeInstanceKeys.ToHashSet();
        List<ProcessInstanceKey> exitedKeys = _entries.Keys
            .Where(key => !activeKeys.Contains(key))
            .ToList();

        foreach (ProcessInstanceKey exitedKey in exitedKeys)
        {
            _entries.Remove(exitedKey);
        }

        return exitedKeys.Count;
    }

    public bool Contains(ProcessInstanceKey instanceKey)
    {
        return _entries.ContainsKey(instanceKey);
    }

    private static bool NeedsUpgrade(
        ProcessEnrichmentInfo enrichment,
        ProcessEnrichmentRequest request)
    {
        return enrichment.Level < request.Level
            || (enrichment.AttemptedFields & request.IdentityFields) != request.IdentityFields;
    }

    private static ProcessEnrichmentInfo Normalize(
        ProcessEnrichmentInfo enrichment,
        ProcessEnrichmentRequest request)
    {
        return enrichment with
        {
            Level = Max(enrichment.Level, request.Level),
            AttemptedFields = enrichment.AttemptedFields | request.IdentityFields
        };
    }

    private static ProcessEnrichmentInfo Merge(
        ProcessEnrichmentInfo previous,
        ProcessEnrichmentInfo current)
    {
        ProcessCompatibilityState compatibility = current.Compatibility == ProcessCompatibilityState.Unknown
            ? previous.Compatibility
            : current.Compatibility;

        return current with
        {
            Compatibility = compatibility,
            Architecture = current.Architecture ?? previous.Architecture,
            ExecutablePath = current.ExecutablePath ?? previous.ExecutablePath,
            PackageIdentity = current.PackageIdentity ?? previous.PackageIdentity,
            PublisherIdentity = current.PublisherIdentity ?? previous.PublisherIdentity,
            IconCacheKey = current.IconCacheKey ?? previous.IconCacheKey,
            Level = Max(previous.Level, current.Level),
            AttemptedFields = previous.AttemptedFields | current.AttemptedFields
        };
    }

    private static ProcessEnrichmentLevel Max(
        ProcessEnrichmentLevel left,
        ProcessEnrichmentLevel right)
    {
        return left >= right ? left : right;
    }

    private sealed record CacheEntry(
        ProcessEnrichmentInfo Enrichment,
        DateTimeOffset LastAttemptAt,
        DateTimeOffset? NextRetryAt,
        int FailureCount);
}
