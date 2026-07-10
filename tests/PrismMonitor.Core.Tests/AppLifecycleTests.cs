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

    [TestMethod]
    public void WindowsProcessProviderDelegatesToSeparateSnapshotAndEnrichmentAdapters()
    {
        string bridgePath = FindRepoFile(Path.Combine(
            "src",
            "PrismMonitor.App",
            "Processes",
            "Win32ProcessInfoProvider.cs"));
        string processesDirectory = Path.GetDirectoryName(bridgePath)!;
        string snapshotProviderPath = Path.Combine(processesDirectory, "Win32ProcessSnapshotProvider.cs");
        string enricherPath = Path.Combine(processesDirectory, "Win32ProcessEnricher.cs");

        Assert.IsTrue(File.Exists(snapshotProviderPath));
        Assert.IsTrue(File.Exists(enricherPath));
        StringAssert.Contains(File.ReadAllText(snapshotProviderPath), "Task.Run");
        StringAssert.Contains(File.ReadAllText(enricherPath), "Task.Run");

        string bridge = File.ReadAllText(bridgePath);
        StringAssert.Contains(bridge, "Win32ProcessSnapshotProvider");
        StringAssert.Contains(bridge, "Win32ProcessEnricher");
        Assert.IsFalse(bridge.Contains("Process.GetProcesses()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AppUsesOneMonitoringHostAndNoNotificationRefreshLoop()
    {
        string appPath = FindRepoFile(Path.Combine("src", "PrismMonitor.App", "App.xaml.cs"));
        string app = File.ReadAllText(appPath);
        string hostPath = Path.Combine(
            Path.GetDirectoryName(appPath)!,
            "Monitoring",
            "MonitoringHost.cs");

        Assert.IsTrue(File.Exists(hostPath));
        string host = File.ReadAllText(hostPath);
        StringAssert.Contains(app, "MonitoringHost");
        StringAssert.Contains(app, "new Win32ProcessSnapshotProvider()");
        StringAssert.Contains(app, "new Win32ProcessEnricher()");
        Assert.IsFalse(app.Contains("_notificationTimer", StringComparison.Ordinal));
        Assert.IsFalse(app.Contains("NotificationTimer_Tick", StringComparison.Ordinal));
        Assert.AreEqual(1, host.Split("new DispatcherTimer", StringSplitOptions.None).Length - 1);
        StringAssert.Contains(host, "RefreshSchedulePolicy.GetRefreshInterval");
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
