using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using PrismMonitor.App.Diagnostics;
using PrismMonitor.App.Monitoring;
using PrismMonitor.App.Notifications;
using PrismMonitor.App.Power;
using PrismMonitor.App.Processes;
using PrismMonitor.App.Tray;
using PrismMonitor.Core.History;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Notifications;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Settings;
using PrismMonitor.Core.Ui;

namespace PrismMonitor.App;

public partial class App : Application
{
    private readonly CompatibilityProcessService _processService = new(new Win32ProcessInfoProvider());
    private readonly IgnoredProcessStore _ignoredProcessStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrismMonitor",
        "ignored-processes.json"));
    private readonly MonitoringSettingsStore _settingsStore = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrismMonitor",
        "settings.json"));
    private readonly LaunchHistoryStore _launchHistoryStore = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrismMonitor",
            "launch-events.jsonl"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrismMonitor",
            "launch-summary.json"));
    private readonly LaunchHistoryRecorder _launchHistoryRecorder = new();
    private readonly CompatibilityProcessNotifier _processNotifier = new();
    private readonly TrayWindowLifetime _windowLifetime = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly MonitoringCoordinator _monitoringCoordinator = new(
        new Win32ProcessSnapshotProvider(),
        new Win32ProcessEnricher());
    private readonly PowerStatusProvider _powerStatusProvider = new();
    private MainWindow? _window;
    private ShellTrayIcon? _trayIcon;
    private CompatibilityProcessToastService? _toastService;
    private MonitoringHost? _monitoringHost;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            StartupDiagnostics.Write("Application.UnhandledException", args.Exception);
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        EnsureMainWindow();
        AppInstance.GetCurrent().Activated += CurrentInstance_Activated;

        _trayIcon = new ShellTrayIcon(
            OpenProcessWindow,
            ExitApplication,
            RequestInteractionRefresh,
            GetTrayStatusAsync);

        _toastService = new CompatibilityProcessToastService(_ignoredProcessStore);
        _toastService.ProcessOpenRequested += ToastService_ProcessOpenRequested;
        _toastService.RulesChanged += ToastService_RulesChanged;
        _toastService.Register();

        _monitoringHost = new MonitoringHost(
            _monitoringCoordinator,
            _ignoredProcessStore,
            _settingsStore,
            _powerStatusProvider,
            _dispatcherQueue);
        _monitoringHost.SnapshotPublished += MonitoringHost_SnapshotPublished;
        _ = StartMonitoringAsync();

        if (TryGetNotificationActivation(AppInstance.GetCurrent().GetActivatedEventArgs(), out NotificationActivation activation))
        {
            HandleNotificationActivation(activation);
        }
    }

    private async Task StartMonitoringAsync()
    {
        try
        {
            if (_monitoringHost is not null)
            {
                await _monitoringHost.StartAsync();
            }
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("App.StartMonitoring", exception);
        }
    }

    private void OpenProcessWindow()
    {
        MainWindow window = EnsureMainWindow();

        _monitoringHost?.SetMainWindowVisible(true);
        window.ShowMainWindow();
        window.Activate();
        ScheduleRefreshProcessWindow(window);
    }

    private async void ExitApplication()
    {
        _windowLifetime.RequestExitClose();
        if (_monitoringHost is not null)
        {
            _monitoringHost.SnapshotPublished -= MonitoringHost_SnapshotPublished;
            await _monitoringHost.DisposeAsync();
        }

        _window?.CloseForExit();
        _trayIcon?.Dispose();
        if (_toastService is not null)
        {
            _toastService.ProcessOpenRequested -= ToastService_ProcessOpenRequested;
            _toastService.RulesChanged -= ToastService_RulesChanged;
        }

        _toastService?.Dispose();
        Exit();
    }

    private Task<TrayStatus> GetTrayStatusAsync()
    {
        IReadOnlyList<CompatibilityProcessInfo> processes =
            _monitoringHost?.LatestSnapshot?.TrayProcesses ?? [];
        return Task.FromResult(CreateTrayStatus(processes));
    }

    private static TrayStatus CreateTrayStatus(IReadOnlyList<CompatibilityProcessInfo> visibleProcesses)
    {
        string topText = TrayTooltipFormatter.FormatTopProcesses(visibleProcesses, 3);
        return new TrayStatus(
            TrayTooltipFormatter.FormatSummary(visibleProcesses),
            topText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            visibleProcesses.Count);
    }

    private MainWindow EnsureMainWindow()
    {
        if (!_windowLifetime.NeedsWindow && _window is not null)
        {
            return _window;
        }

        _window = new MainWindow(
            _processService,
            _ignoredProcessStore,
            _settingsStore,
            _launchHistoryStore);
        _windowLifetime.MarkWindowCreated();
        _window.HiddenToTray += (_, _) =>
        {
            _windowLifetime.MarkHiddenToTray();
            _monitoringHost?.SetMainWindowVisible(false);
        };
        _window.Closed += (_, _) =>
        {
            _windowLifetime.MarkWindowClosed();
            _monitoringHost?.SetMainWindowVisible(false);
            if (_windowLifetime.NeedsWindow)
            {
                _window = null;
            }
        };

        return _window;
    }

    private static void ScheduleRefreshProcessWindow(MainWindow window)
    {
        _ = window.DispatcherQueue.TryEnqueue(async () => await RefreshProcessWindowAsync(window));
    }

    private static async Task RefreshProcessWindowAsync(MainWindow window)
    {
        try
        {
            await window.RefreshAsync();
        }
        catch
        {
            // The temporary MainWindow pull bridge is removed in the shared-feed task.
        }
    }

    private void CurrentInstance_Activated(object? sender, AppActivationArguments args)
    {
        if (TryGetNotificationActivation(args, out NotificationActivation activation))
        {
            HandleNotificationActivation(activation);
            return;
        }

        _ = _dispatcherQueue.TryEnqueue(OpenProcessWindow);
    }

    private void ToastService_ProcessOpenRequested(object? sender, NotificationProcessOpenRequestedEventArgs e)
    {
        _ = _dispatcherQueue.TryEnqueue(async () => await OpenNotificationTargetAsync(e.ProcessId, e.ProcessName));
    }

    private void ToastService_RulesChanged(object? sender, EventArgs e)
    {
        _ = _dispatcherQueue.TryEnqueue(async () =>
        {
            if (_monitoringHost is not null)
            {
                await _monitoringHost.ReloadConfigurationAsync();
            }
        });
    }

    private void HandleNotificationActivation(NotificationActivation activation)
    {
        if (_toastService is not null)
        {
            _ = _toastService.HandleActivationAsync(activation);
            return;
        }

        if (activation.Kind == NotificationActivationKind.OpenProcess
            && activation.ProcessId is int processId
            && activation.ProcessName is string processName)
        {
            _ = _dispatcherQueue.TryEnqueue(async () => await OpenNotificationTargetAsync(processId, processName));
        }
    }

    private static bool TryGetNotificationActivation(
        AppActivationArguments args,
        out NotificationActivation activation)
    {
        if (args.Kind == ExtendedActivationKind.AppNotification
            && args.Data is AppNotificationActivatedEventArgs notificationArgs)
        {
            activation = NotificationActivationParser.Parse(
                new Dictionary<string, string>(notificationArgs.Arguments));
            return activation.Kind != NotificationActivationKind.None;
        }

        activation = NotificationActivation.None;
        return false;
    }

    private async Task OpenNotificationTargetAsync(int processId, string processName)
    {
        MainWindow window = EnsureMainWindow();

        _monitoringHost?.SetMainWindowVisible(true);
        window.ShowMainWindow();
        window.Activate();

        if (_monitoringHost is not null)
        {
            await _monitoringHost.RequestRefreshAsync(
                MonitoringRefreshReason.WindowVisible,
                fullDetails: true);
        }

        MonitoringSnapshot? snapshot = _monitoringHost?.LatestSnapshot;
        CompatibilityProcessInfo? currentProcess = snapshot?.Processes.FirstOrDefault(
            process => process.ProcessId == processId
                && string.Equals(process.Name, processName, StringComparison.OrdinalIgnoreCase));

        if (currentProcess is not null && snapshot is not null)
        {
            await window.ShowProcessesAndFocusAsync(processId, snapshot.Processes);
            return;
        }

        await window.ShowHistoryForProcessAsync(processName);
    }

    private void RequestInteractionRefresh()
    {
        if (_monitoringHost is not null)
        {
            _ = _monitoringHost.RequestRefreshAsync(MonitoringRefreshReason.Interaction);
        }
    }

    private void MonitoringHost_SnapshotPublished(object? sender, MonitoringSnapshot snapshot)
    {
        _ = _dispatcherQueue.TryEnqueue(() => ApplySnapshotToSurfaces(snapshot));
    }

    private async void ApplySnapshotToSurfaces(MonitoringSnapshot snapshot)
    {
        Task historyWrite = TryRecordHistoryAsync(snapshot.HistoryProcesses);
        TryUpdateTray(snapshot.TrayProcesses);
        TryNotify(snapshot.NotifiableProcesses);
        await historyWrite;
    }

    private async Task TryRecordHistoryAsync(IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        try
        {
            IReadOnlyList<LaunchHistoryEvent> events = _launchHistoryRecorder.CaptureNewEvents(processes);
            await _launchHistoryStore.AppendRangeAsync(events);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("App.RecordHistory", exception);
        }
    }

    private void TryUpdateTray(IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        try
        {
            _trayIcon?.UpdateStatus(CreateTrayStatus(processes));
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("App.UpdateTray", exception);
        }
    }

    private void TryNotify(IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        try
        {
            IReadOnlyList<CompatibilityProcessInfo> newProcesses = _processNotifier.CaptureNewProcesses(processes);
            foreach (CompatibilityProcessInfo process in newProcesses)
            {
                _toastService?.ShowNewProcess(process);
            }
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("App.Notify", exception);
        }
    }
}
