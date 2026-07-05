using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Processes;

public static class ArchitectureProcessFilter
{
    public static IReadOnlyList<CompatibilityProcessInfo> FilterVisibleProcesses(
        IEnumerable<CompatibilityProcessInfo> processes,
        MonitoringSettings settings)
    {
        return processes
            .Where(process => settings.IncludeArm64EcProcesses || !IsArm64EcFamily(process.Architecture))
            .ToList();
    }

    public static IReadOnlyList<CompatibilityProcessInfo> FilterNotifiableProcesses(
        IEnumerable<CompatibilityProcessInfo> processes,
        MonitoringSettings settings)
    {
        return FilterVisibleProcesses(processes, settings)
            .Where(process => IsIncludedByNotificationLevel(process.Architecture, settings.NotificationLevel))
            .ToList();
    }

    private static bool IsIncludedByNotificationLevel(string architecture, NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.X86Only => IsX86(architecture),
            NotificationLevel.X86AndX64 => IsX86(architecture) || IsX64(architecture),
            NotificationLevel.X86X64AndArm64Ec => IsX86(architecture) || IsX64(architecture) || IsArm64EcFamily(architecture),
            _ => false
        };
    }

    private static bool IsX86(string architecture)
    {
        return string.Equals(architecture, "x86", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsX64(string architecture)
    {
        return string.Equals(architecture, "x64", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArm64EcFamily(string architecture)
    {
        return string.Equals(architecture, "ARM64EC", StringComparison.OrdinalIgnoreCase)
            || string.Equals(architecture, "ARM64X", StringComparison.OrdinalIgnoreCase);
    }
}
