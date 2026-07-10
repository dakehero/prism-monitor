namespace PrismMonitor.Core.Ui;

public sealed class LatestAsyncWorkGate<T>
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _latestRequest;

    public async Task RunAsync(T value, Func<T, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        long request = Interlocked.Increment(ref _latestRequest);
        await _gate.WaitAsync();
        try
        {
            if (request != Volatile.Read(ref _latestRequest))
            {
                return;
            }

            await operation(value);
        }
        finally
        {
            _gate.Release();
        }
    }
}
