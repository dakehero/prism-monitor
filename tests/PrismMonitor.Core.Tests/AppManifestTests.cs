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
    public void PackageManifestAllowsElevation()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "Package.appxmanifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "Capability Name=\"runFullTrust\"");
        StringAssert.Contains(manifest, "Capability Name=\"allowElevation\"");
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
