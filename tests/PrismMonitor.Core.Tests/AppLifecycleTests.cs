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
    public void WindowsProcessAdaptersSeparateSnapshotAndEnrichmentWork()
    {
        string snapshotProviderPath = FindRepoFile(Path.Combine(
            "src",
            "PrismMonitor.App",
            "Processes",
            "Win32ProcessSnapshotProvider.cs"));
        string processesDirectory = Path.GetDirectoryName(snapshotProviderPath)!;
        string enricherPath = Path.Combine(processesDirectory, "Win32ProcessEnricher.cs");

        Assert.IsTrue(File.Exists(enricherPath));
        StringAssert.Contains(File.ReadAllText(snapshotProviderPath), "Task.Run");
        StringAssert.Contains(File.ReadAllText(enricherPath), "Task.Run");
    }

    [TestMethod]
    public void RepositoryHasOneProcessCapturePathAndNoLegacyPullService()
    {
        string root = Path.GetDirectoryName(FindRepoFile("PrismMonitor.slnx"))!;
        string[] directEnumerationFiles = Directory
            .GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("Process.GetProcesses()", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "Win32ProcessSnapshotProvider.cs" }, directEnumerationFiles);
        Assert.IsFalse(File.Exists(Path.Combine(
            root, "src", "PrismMonitor.Core", "Processes", "CompatibilityProcessService.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(
            root, "src", "PrismMonitor.Core", "Processes", "IProcessInfoProvider.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(
            root, "src", "PrismMonitor.App", "Processes", "Win32ProcessInfoProvider.cs")));
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
        StringAssert.Contains(host, "await _coordinator.StopAsync()");
    }

    [TestMethod]
    public void MainWindowConsumesSnapshotsWithoutOwningProcessTimerOrService()
    {
        string source = File.ReadAllText(FindRepoFile(Path.Combine(
            "src",
            "PrismMonitor.App",
            "MainWindow.xaml.cs")));

        Assert.IsFalse(source.Contains("CompatibilityProcessService", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("_refreshTimer", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("RefreshTimer_Tick", StringComparison.Ordinal));
        StringAssert.Contains(source, "ApplyMonitoringSnapshotAsync");
        StringAssert.Contains(source, "RefreshRequested");
        StringAssert.Contains(source, "ConfigurationChanged");
    }

    [TestMethod]
    public void AppSnapshotFanoutIsolatesEverySurfaceFailure()
    {
        string source = File.ReadAllText(FindRepoFile(Path.Combine(
            "src",
            "PrismMonitor.App",
            "App.xaml.cs")));

        StringAssert.Contains(source, "App.RecordHistory");
        StringAssert.Contains(source, "App.UpdateTray");
        StringAssert.Contains(source, "App.Notify");
        StringAssert.Contains(source, "App.UpdateMainWindow");
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
