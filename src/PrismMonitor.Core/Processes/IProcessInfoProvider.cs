namespace PrismMonitor.Core.Processes;

public interface IProcessInfoProvider
{
    Task<IReadOnlyList<CompatibilityProcessInfo>> GetCompatibilityProcessesAsync(CancellationToken cancellationToken = default);
}
