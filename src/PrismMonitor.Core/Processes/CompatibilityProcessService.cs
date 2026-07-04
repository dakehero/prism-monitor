namespace PrismMonitor.Core.Processes;

public sealed class CompatibilityProcessService(IProcessInfoProvider provider)
{
    public async Task<IReadOnlyList<CompatibilityProcessInfo>> GetCurrentProcessesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<CompatibilityProcessInfo> processes = await provider
                .GetCompatibilityProcessesAsync(cancellationToken)
                .ConfigureAwait(false);

            return processes
                .OrderByDescending(process => process.CpuTime)
                .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(process => process.ProcessId)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
