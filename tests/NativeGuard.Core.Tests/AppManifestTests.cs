namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class AppManifestTests
{
    [TestMethod]
    public void AppManifestDoesNotRequestAdministratorElevation()
    {
        string manifestPath = FindRepoFile(Path.Combine("src", "NativeGuard.App", "app.manifest"));
        string manifest = File.ReadAllText(manifestPath);

        StringAssert.Contains(manifest, "requestedExecutionLevel level=\"asInvoker\"");
        Assert.IsFalse(
            manifest.Contains("requestedExecutionLevel level=\"requireAdministrator\"", StringComparison.Ordinal),
            "MSIX activation fails when the app executable requests administrator elevation.");
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
