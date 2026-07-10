using PrismMonitor.Core.Processes;

namespace PrismMonitor.Core.Monitoring;

public sealed record MonitoringSnapshot(
    IReadOnlyList<CompatibilityProcessInfo> Processes,
    IReadOnlyList<CompatibilityProcessInfo> TrayProcesses,
    IReadOnlyList<CompatibilityProcessInfo> NotifiableProcesses,
    IReadOnlyList<CompatibilityProcessInfo> HistoryProcesses,
    long Sequence = 0,
    DateTimeOffset CapturedAt = default);
