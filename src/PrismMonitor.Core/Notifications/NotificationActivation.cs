namespace PrismMonitor.Core.Notifications;

public sealed record NotificationActivation(
    NotificationActivationKind Kind,
    int? ProcessId = null,
    string? ProcessName = null)
{
    public static NotificationActivation None { get; } = new(NotificationActivationKind.None);
}

public enum NotificationActivationKind
{
    None,
    OpenProcess,
    TerminateProcess,
    IgnoreProcess
}
