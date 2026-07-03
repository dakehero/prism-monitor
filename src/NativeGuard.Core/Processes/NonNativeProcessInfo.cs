namespace NativeGuard.Core.Processes;

public sealed record NonNativeProcessInfo(
    string Name,
    int ProcessId,
    string Architecture,
    TimeSpan CpuTime);
