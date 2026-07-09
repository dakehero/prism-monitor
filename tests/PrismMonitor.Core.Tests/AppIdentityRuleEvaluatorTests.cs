using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class AppIdentityRuleEvaluatorTests
{
    [TestMethod]
    public void IsSuppressed_MatchesProcessNameCaseInsensitively()
    {
        AppIdentityRule rule = new(
            "Ignore Chrome",
            ProcessName: "Chrome",
            Targets: SuppressionTarget.All);

        bool result = AppIdentityRuleEvaluator.IsSuppressed(
            new AppIdentity("chrome.exe", Architecture: "x64"),
            [rule],
            SuppressionTarget.Processes);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSuppressed_MatchesExecutablePathCaseInsensitively()
    {
        AppIdentityRule rule = new(
            "Ignore Apple Music",
            ExecutablePath: @"C:\Program Files\WindowsApps\AppleMusic.exe",
            Targets: SuppressionTarget.All);

        bool result = AppIdentityRuleEvaluator.IsSuppressed(
            new AppIdentity("AppleMusic", ExecutablePath: @"c:\program files\windowsapps\APPLEMUSIC.EXE"),
            [rule],
            SuppressionTarget.Toast);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsSuppressed_RequiresAllConfiguredMatchFields()
    {
        AppIdentityRule rule = new(
            "Ignore Specific Chrome",
            ProcessName: "Chrome",
            ExecutablePath: @"C:\Apps\Chrome.exe",
            Targets: SuppressionTarget.All);

        bool result = AppIdentityRuleEvaluator.IsSuppressed(
            new AppIdentity("Chrome", ExecutablePath: @"C:\Other\Chrome.exe"),
            [rule],
            SuppressionTarget.Tray);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsSuppressed_RespectsSuppressionTarget()
    {
        AppIdentityRule rule = new(
            "Mute Toasts",
            ProcessName: "LegacyTool",
            Targets: SuppressionTarget.Toast);

        AppIdentity identity = new("LegacyTool", Architecture: "x86");

        Assert.IsTrue(AppIdentityRuleEvaluator.IsSuppressed(identity, [rule], SuppressionTarget.Toast));
        Assert.IsFalse(AppIdentityRuleEvaluator.IsSuppressed(identity, [rule], SuppressionTarget.Processes));
    }

    [TestMethod]
    public void IsSuppressed_DoesNotMatchMissingMetadataUnlessAnotherFieldMatches()
    {
        AppIdentityRule pathOnlyRule = new(
            "Path only",
            ExecutablePath: @"C:\Apps\Tool.exe",
            Targets: SuppressionTarget.All);
        AppIdentityRule nameRule = new(
            "Name fallback",
            ProcessName: "Tool",
            Targets: SuppressionTarget.All);

        AppIdentity partialIdentity = new("Tool");

        Assert.IsFalse(AppIdentityRuleEvaluator.IsSuppressed(partialIdentity, [pathOnlyRule], SuppressionTarget.History));
        Assert.IsTrue(AppIdentityRuleEvaluator.IsSuppressed(partialIdentity, [nameRule], SuppressionTarget.History));
    }

    [TestMethod]
    public void FilterProcesses_CarriesPackageAndPublisherIdentity()
    {
        AppIdentityRule packageRule = new(
            "Packaged app",
            PackageIdentity: "Contoso.Package_1.0.0.0_arm64__abc",
            PublisherIdentity: "CN=Contoso",
            Targets: SuppressionTarget.Processes);
        CompatibilityProcessInfo[] processes =
        [
            new(
                "PackagedApp",
                10,
                "ARM64EC",
                TimeSpan.FromSeconds(1),
                PackageIdentity: "contoso.package_1.0.0.0_arm64__abc",
                PublisherIdentity: "cn=contoso"),
            new("OtherApp", 20, "x64", TimeSpan.FromSeconds(1))
        ];

        IReadOnlyList<CompatibilityProcessInfo> result = AppIdentityRuleFilter.FilterProcesses(
            processes,
            [packageRule],
            SuppressionTarget.Processes);

        CollectionAssert.AreEqual(new[] { "OtherApp" }, result.Select(process => process.Name).ToArray());
    }
}
