namespace PrismMonitor.Core.Processes;

public sealed class CompatibilityProcessService(
    IProcessInfoProvider provider,
    TimeSpan? cacheDuration = null,
    Func<DateTimeOffset>? clock = null)
{
    private readonly TimeSpan _cacheDuration = cacheDuration ?? TimeSpan.FromSeconds(2);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<CompatibilityProcessInfo>? _cachedProcesses;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<CompatibilityProcessInfo>> GetCurrentProcessesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DateTimeOffset now = _clock();
            if (_cachedProcesses is not null && now - _cachedAt < _cacheDuration)
            {
                return _cachedProcesses;
            }

            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                now = _clock();
                if (_cachedProcesses is not null && now - _cachedAt < _cacheDuration)
                {
                    return _cachedProcesses;
                }

                IReadOnlyList<CompatibilityProcessInfo> processes = await provider
                    .GetCompatibilityProcessesAsync(cancellationToken)
                    .ConfigureAwait(false);

                IReadOnlyList<CompatibilityProcessInfo> sortedProcesses = processes
                    .OrderByDescending(process => process.CpuTime)
                    .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(process => process.ProcessId)
                    .ToList();

                _cachedProcesses = sortedProcesses;
                _cachedAt = _clock();
                return sortedProcesses;
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch
        {
            return [];
        }
    }
}
