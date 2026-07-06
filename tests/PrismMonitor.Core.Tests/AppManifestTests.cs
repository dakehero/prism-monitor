namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class AppManifestTests
{
    [TestMethod]
    public void AppManifestRunsWithoutElevationByDefault()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "app.manifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "requestedExecutionLevel level=\"asInvoker\"");
        Assert.IsFalse(manifest.Contains("requestedExecutionLevel level=\"requireAdministrator\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PackageManifestUsesOnlyRequiredRestrictedCapabilities()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "Package.appxmanifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "Capability Name=\"runFullTrust\"");
        Assert.IsFalse(manifest.Contains("Capability Name=\"allowElevation\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PackageManifestUsesReservedStoreIdentity()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "Package.appxmanifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "Name=\"dakehero.prism-monitor\"");
        StringAssert.Contains(manifest, "Publisher=\"CN=ED5CCA99-70D2-44C7-8831-B3B54CCF6448\"");
        StringAssert.Contains(manifest, "<DisplayName>prism-monitor</DisplayName>");
        StringAssert.Contains(manifest, "DisplayName=\"prism-monitor\"");
        Assert.IsFalse(manifest.Contains("<DisplayName>Prism Monitor</DisplayName>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PackageManifestRegistersToastActivation()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "Package.appxmanifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "Category=\"windows.toastNotificationActivation\"");
        StringAssert.Contains(manifest, "ToastActivatorCLSID=\"e8c3c16d-8b72-4cb1-8f5e-42dfcf6f0dc7\"");
        StringAssert.Contains(manifest, "Category=\"windows.comServer\"");
        StringAssert.Contains(manifest, "Executable=\"PrismMonitor.App.exe\"");
        StringAssert.Contains(manifest, "Arguments=\"----AppNotificationActivated:\"");
        StringAssert.Contains(manifest, "Class Id=\"e8c3c16d-8b72-4cb1-8f5e-42dfcf6f0dc7\"");
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
