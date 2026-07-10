using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Monitoring;

public interface IProcessEnricher
{
    Task<ProcessEnrichmentInfo> EnrichAsync(
        ProcessSnapshotInfo snapshot,
        ProcessEnrichmentRequest request,
        CancellationToken cancellationToken = default);
}
