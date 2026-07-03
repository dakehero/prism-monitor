namespace NativeGuard.Core.Processes;

public interface IProcessInfoProvider
{
    Task<IReadOnlyList<NonNativeProcessInfo>> GetNonNativeProcessesAsync(CancellationToken cancellationToken = default);
}
