namespace PrismMonitor.App.Tray;

internal sealed record TrayStatus(string Tooltip, IReadOnlyList<string> TopProcesses, int ProcessCount);
