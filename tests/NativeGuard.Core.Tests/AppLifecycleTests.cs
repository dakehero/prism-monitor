namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class AppLifecycleTests
{
    [TestMethod]
    public void AppProjectUsesCustomProgramEntryPoint()
    {
        string projectPath = FindRepoFile(Path.Combine("src", "NativeGuard.App", "NativeGuard.App.csproj"));
        string project = File.ReadAllText(projectPath);

        StringAssert.Contains(project, "DISABLE_XAML_GENERATED_MAIN");
    }

    [TestMethod]
    public void ProgramRedirectsSecondaryActivationsToMainInstance()
    {
        string programPath = FindRepoFile(Path.Combine("src", "NativeGuard.App", "Program.cs"));
        string program = File.ReadAllText(programPath);

        StringAssert.Contains(program, "AppInstance.FindOrRegisterForKey");
        StringAssert.Contains(program, "RedirectActivationToAsync");
        StringAssert.Contains(program, "Application.Start");
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
