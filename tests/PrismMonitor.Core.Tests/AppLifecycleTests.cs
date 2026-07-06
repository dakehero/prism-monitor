namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class AppLifecycleTests
{
    [TestMethod]
    public void AppProjectUsesCustomProgramEntryPoint()
    {
        string projectPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "PrismMonitor.App.csproj"));
        string project = File.ReadAllText(projectPath);

        StringAssert.Contains(project, "DISABLE_XAML_GENERATED_MAIN");
    }

    [TestMethod]
    public void ProgramRedirectsSecondaryActivationsToMainInstance()
    {
        string programPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "Program.cs"));
        string program = File.ReadAllText(programPath);

        StringAssert.Contains(program, "AppInstance.FindOrRegisterForKey");
        StringAssert.Contains(program, "RedirectActivationToAsync");
        StringAssert.Contains(program, "Application.Start");
    }

    [TestMethod]
    public void AppHandlesNotificationActivationArguments()
    {
        string appPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "App.xaml.cs"));
        string app = File.ReadAllText(appPath);

        StringAssert.Contains(app, "ExtendedActivationKind.AppNotification");
        StringAssert.Contains(app, "AppNotificationActivatedEventArgs");
        StringAssert.Contains(app, "NotificationActivationParser.Parse");
        StringAssert.Contains(app, "OpenNotificationTargetAsync");
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
