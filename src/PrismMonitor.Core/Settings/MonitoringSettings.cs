namespace PrismMonitor.Core.Settings;

public enum NotificationLevel
{
    X86Only,
    X86AndX64,
    X86X64AndArm64Ec
}

public sealed record MonitoringSettings(bool IncludeArm64EcProcesses, NotificationLevel NotificationLevel)
{
    public static MonitoringSettings Default { get; } = new(
        IncludeArm64EcProcesses: true,
        NotificationLevel: NotificationLevel.X86X64AndArm64Ec);
}
