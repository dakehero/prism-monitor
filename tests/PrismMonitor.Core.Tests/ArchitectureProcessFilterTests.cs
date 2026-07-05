using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Settings;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class ArchitectureProcessFilterTests
{
    [TestMethod]
    public void FilterVisibleProcesses_RemovesArm64EcAndArm64X_WhenDisabled()
    {
        CompatibilityProcessInfo[] processes =
        [
            Create("legacy32", "x86"),
            Create("legacy64", "x64"),
            Create("hybrid", "ARM64EC"),
            Create("hybridx", "ARM64X")
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = ArchitectureProcessFilter.FilterVisibleProcesses(
            processes,
            new MonitoringSettings(IncludeArm64EcProcesses: false, NotificationLevel: NotificationLevel.X86X64AndArm64Ec));

        CollectionAssert.AreEqual(new[] { "legacy32", "legacy64" }, result.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    [DataRow(NotificationLevel.X86Only, new[] { "legacy32" })]
    [DataRow(NotificationLevel.X86AndX64, new[] { "legacy32", "legacy64" })]
    [DataRow(NotificationLevel.X86X64AndArm64Ec, new[] { "legacy32", "legacy64", "hybrid", "hybridx" })]
    public void FilterNotifiableProcesses_AppliesNotificationLevel(NotificationLevel level, string[] expectedNames)
    {
        CompatibilityProcessInfo[] processes =
        [
            Create("legacy32", "x86"),
            Create("legacy64", "x64"),
            Create("hybrid", "ARM64EC"),
            Create("hybridx", "ARM64X")
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = ArchitectureProcessFilter.FilterNotifiableProcesses(
            processes,
            new MonitoringSettings(IncludeArm64EcProcesses: true, NotificationLevel: level));

        CollectionAssert.AreEqual(expectedNames, result.Select(process => process.Name).ToArray());
    }

    [TestMethod]
    public void FilterNotifiableProcesses_StillExcludesArm64Ec_WhenArm64EcVisibilityIsDisabled()
    {
        CompatibilityProcessInfo[] processes =
        [
            Create("legacy32", "x86"),
            Create("legacy64", "x64"),
            Create("hybrid", "ARM64EC")
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = ArchitectureProcessFilter.FilterNotifiableProcesses(
            processes,
            new MonitoringSettings(IncludeArm64EcProcesses: false, NotificationLevel: NotificationLevel.X86X64AndArm64Ec));

        CollectionAssert.AreEqual(new[] { "legacy32", "legacy64" }, result.Select(process => process.Name).ToArray());
    }

    private static CompatibilityProcessInfo Create(string name, string architecture)
    {
        return new CompatibilityProcessInfo(name, ProcessId: name.GetHashCode(), architecture, TimeSpan.Zero, ExecutablePath: null);
    }
}
