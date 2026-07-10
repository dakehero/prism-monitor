using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Monitoring;

public interface IProcessSnapshotProvider
{
    Task<IReadOnlyList<ProcessObservation>> CaptureAsync(CancellationToken cancellationToken = default);
}
