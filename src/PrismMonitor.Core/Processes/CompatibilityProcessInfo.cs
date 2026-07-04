namespace PrismMonitor.Core.Processes;

public sealed record CompatibilityProcessInfo(
    string Name,
    int ProcessId,
    string Architecture,
    TimeSpan CpuTime,
    string? ExecutablePath = null);
