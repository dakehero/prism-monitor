namespace NativeGuard.Core.Processes;

public sealed class NonNativeProcessService(IProcessInfoProvider provider)
{
    public async Task<IReadOnlyList<NonNativeProcessInfo>> GetCurrentProcessesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<NonNativeProcessInfo> processes = await provider
                .GetNonNativeProcessesAsync(cancellationToken)
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
