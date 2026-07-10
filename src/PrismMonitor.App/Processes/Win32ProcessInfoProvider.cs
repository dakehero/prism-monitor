using PrismMonitor.Core.Processes;

namespace PrismMonitor.App.Processes;

internal sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    private readonly Win32ProcessSnapshotProvider _snapshotProvider = new();
    private readonly Win32ProcessEnricher _processEnricher = new();
    private readonly ProcessSnapshotTracker _snapshotTracker = new();

    public async Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessObservation> observations = await _snapshotProvider
            .CaptureAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ProcessSnapshotInfo> snapshots = _snapshotTracker.Track(
            observations,
            DateTimeOffset.UtcNow);
        List<CompatibilityProcessInfo> processes = [];

        foreach (ProcessSnapshotInfo snapshot in snapshots)
        {
            ProcessEnrichmentInfo enrichment = await _processEnricher
                .EnrichAsync(snapshot, ProcessEnrichmentRequest.Full, cancellationToken)
                .ConfigureAwait(false);
            if (enrichment.Compatibility != ProcessCompatibilityState.Compatible)
            {
                continue;
            }

            processes.Add(new CompatibilityProcessInfo(
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
                enrichment.HasLimitedDetails));
        }

        return processes;
    }
}
