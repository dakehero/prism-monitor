namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class MainWindowDefaultsTests
{
    [TestMethod]
    public void MainWindowDefaultSizeMatchesPrimaryDisplayTarget()
    {
        string code = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml.cs")));

        StringAssert.Contains(code, "new(1636, 975)");
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
