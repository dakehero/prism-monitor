using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class MonitoringSnapshotBuilderTests
{
    [TestMethod]
    public void Build_FansOutSingleIncludedProcessSetToSurfaceSpecificRules()
    {
        CompatibilityProcessInfo[] processes =
        [
            new("VisibleOnly", 1, "x64", TimeSpan.FromSeconds(3)),
            new("MutedToast", 2, "x86", TimeSpan.FromSeconds(2)),
            new("HiddenTray", 3, "x64", TimeSpan.FromSeconds(1)),
            new("HistoryMuted", 4, "x64", TimeSpan.FromSeconds(4))
        ];
        AppIdentityRule[] rules =
        [
            new("No toast", ProcessName: "MutedToast", Targets: SuppressionTarget.Toast),
            new("No tray", ProcessName: "HiddenTray", Targets: SuppressionTarget.Tray),
            new("No history", ProcessName: "HistoryMuted", Targets: SuppressionTarget.History)
        ];

        MonitoringSnapshot snapshot = MonitoringSnapshotBuilder.Build(
            processes,
            rules,
            MonitoringSettings.Default);

        CollectionAssert.AreEqual(
            new[] { "VisibleOnly", "MutedToast", "HiddenTray", "HistoryMuted" },
            snapshot.Processes.Select(process => process.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "VisibleOnly", "MutedToast", "HistoryMuted" },
            snapshot.TrayProcesses.Select(process => process.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "VisibleOnly", "MutedToast", "HiddenTray" },
            snapshot.HistoryProcesses.Select(process => process.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "VisibleOnly", "HiddenTray", "HistoryMuted" },
            snapshot.NotifiableProcesses.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public void Build_AppliesArchitectureSettingsBeforeRuleTargets()
    {
        CompatibilityProcessInfo[] processes =
        [
            new("Legacy32", 1, "x86", TimeSpan.FromSeconds(3)),
            new("Hybrid", 2, "ARM64EC", TimeSpan.FromSeconds(2)),
            new("Legacy64", 3, "x64", TimeSpan.FromSeconds(1))
        ];
        MonitoringSettings settings = new(
            IncludeArm64EcProcesses: false,
            NotificationLevel: NotificationLevel.X86Only);

        MonitoringSnapshot snapshot = MonitoringSnapshotBuilder.Build(processes, [], settings);

        CollectionAssert.AreEqual(
            new[] { "Legacy32", "Legacy64" },
            snapshot.Processes.Select(process => process.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Legacy32" },
            snapshot.NotifiableProcesses.Select(process => process.Name).ToArray());
    }
}
