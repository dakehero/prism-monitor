namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class AppManifestTests
{
    [TestMethod]
    public void AppManifestRequestsAdministratorElevation()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "NativeGuard.App", "app.manifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "requestedExecutionLevel level=\"requireAdministrator\"");
    }

    [TestMethod]
    public void PackageManifestAllowsElevation()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "NativeGuard.App", "Package.appxmanifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "Capability Name=\"runFullTrust\"");
        StringAssert.Contains(manifest, "Capability Name=\"allowElevation\"");
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
