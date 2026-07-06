using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using PrismMonitor.Core.History;
using PrismMonitor.Core.Notifications;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Power;
using PrismMonitor.Core.Settings;
using PrismMonitor.Core.Ui;
using PrismMonitor.App.Diagnostics;
using PrismMonitor.App.Elevation;
using PrismMonitor.App.Notifications;
using PrismMonitor.App.Power;
using PrismMonitor.App.Processes;
using PrismMonitor.App.Tray;

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
    private readonly PowerStatusProvider _powerStatusProvider = new();
    private readonly CompatibilityProcessNotifier _processNotifier = new();
    private readonly TrayWindowLifetime _windowLifetime = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherTimer _notificationTimer = new();
    private MainWindow? _window;
    private ShellTrayIcon? _trayIcon;
    private CompatibilityProcessToastService? _toastService;
    private bool _isNotificationRefreshRunning;
    private bool _isMainWindowVisible;
    private DateTimeOffset _lastInteractionRefresh = DateTimeOffset.MinValue;
    private readonly bool _isElevated = ElevationHelper.IsCurrentProcessElevated();

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

        if (!_isElevated)
        {
            _toastService = new CompatibilityProcessToastService(_ignoredProcessStore);
            _toastService.ProcessOpenRequested += ToastService_ProcessOpenRequested;
            _toastService.Register();
        }
        _powerStatusProvider.PowerSourceChanged += PowerStatusProvider_PowerSourceChanged;
        _notificationTimer.Interval = TimeSpan.FromSeconds(1);
        _notificationTimer.Tick += NotificationTimer_Tick;
        UpdateNotificationTimer();
        RequestInteractionRefresh();

        if (TryGetNotificationActivation(AppInstance.GetCurrent().GetActivatedEventArgs(), out NotificationActivation activation))
        {
            HandleNotificationActivation(activation);
        }
    }

    private void OpenProcessWindow()
    {
        MainWindow window = EnsureMainWindow();

        _isMainWindowVisible = true;
        UpdateNotificationTimer();
        window.ShowMainWindow();
        window.Activate();
        ScheduleRefreshProcessWindow(window);
    }

    private void ExitApplication()
    {
        _windowLifetime.RequestExitClose();
        _notificationTimer.Stop();
        _powerStatusProvider.PowerSourceChanged -= PowerStatusProvider_PowerSourceChanged;
        _powerStatusProvider.Dispose();
        _window?.CloseForExit();
        _trayIcon?.Dispose();
        if (_toastService is not null)
        {
            _toastService.ProcessOpenRequested -= ToastService_ProcessOpenRequested;
        }

        _toastService?.Dispose();
        Exit();
    }

    private async Task<TrayStatus> GetTrayStatusAsync()
    {
        IReadOnlyList<CompatibilityProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        MonitoringSettings settings = await _settingsStore.GetAsync();
        IReadOnlyList<CompatibilityProcessInfo> visibleProcesses = ArchitectureProcessFilter.FilterVisibleProcesses(
            IgnoredProcessFilter.Filter(processes, ignoredNames),
            settings);
        return CreateTrayStatus(visibleProcesses);
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
            _launchHistoryStore,
            _isElevated,
            RelaunchAsAdministrator);
        _windowLifetime.MarkWindowCreated();
        _window.HiddenToTray += (_, _) =>
        {
            _windowLifetime.MarkHiddenToTray();
            _isMainWindowVisible = false;
            UpdateNotificationTimer();
        };
        _window.Closed += (_, _) =>
        {
            _windowLifetime.MarkWindowClosed();
            _isMainWindowVisible = false;
            UpdateNotificationTimer();
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
            // Keep tray callbacks from terminating the app if a refresh fails.
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

        _isMainWindowVisible = true;
        UpdateNotificationTimer();
        window.ShowMainWindow();
        window.Activate();

        MonitoringSnapshot snapshot = await Task.Run(ReadMonitoringSnapshotAsync);
        CompatibilityProcessInfo? currentProcess = snapshot.VisibleProcesses.FirstOrDefault(
            process => process.ProcessId == processId
                && string.Equals(process.Name, processName, StringComparison.OrdinalIgnoreCase));

        if (currentProcess is not null)
        {
            await window.ShowProcessesAndFocusAsync(processId, snapshot.VisibleProcesses);
            return;
        }

        await window.ShowHistoryForProcessAsync(processName);
    }

    private bool RelaunchAsAdministrator()
    {
        bool started = ElevationHelper.TryRelaunchCurrentProcessAsAdministrator();
        if (started)
        {
            ExitApplication();
        }

        return started;
    }

    private void PowerStatusProvider_PowerSourceChanged(object? sender, EventArgs e)
    {
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            UpdateNotificationTimer();
            RequestInteractionRefresh();
        });
    }

    private void UpdateNotificationTimer()
    {
        RefreshMode refreshMode = RefreshSchedulePolicy.GetRefreshMode(
            _powerStatusProvider.GetCurrentPowerSource(),
            _isMainWindowVisible);

        if (refreshMode == RefreshMode.PeriodicBackground)
        {
            _notificationTimer.Start();
            return;
        }

        _notificationTimer.Stop();
    }

    private void RequestInteractionRefresh()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastInteractionRefresh < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastInteractionRefresh = now;
        _ = _dispatcherQueue.TryEnqueue(() => NotificationTimer_Tick(null, new object()));
    }

    private async void NotificationTimer_Tick(object? sender, object e)
    {
        if (_isNotificationRefreshRunning)
        {
            return;
        }

        _isNotificationRefreshRunning = true;
        try
        {
            MonitoringSnapshot snapshot = await Task.Run(ReadMonitoringSnapshotAsync);

            IReadOnlyList<LaunchHistoryEvent> historyEvents = _launchHistoryRecorder.CaptureNewEvents(
                snapshot.VisibleProcesses,
                DateTimeOffset.UtcNow);
            foreach (LaunchHistoryEvent historyEvent in historyEvents)
            {
                await _launchHistoryStore.AppendAsync(historyEvent);
            }

            _trayIcon?.UpdateStatus(CreateTrayStatus(snapshot.VisibleProcesses));
            IReadOnlyList<CompatibilityProcessInfo> newProcesses = _processNotifier.CaptureNewProcesses(snapshot.NotifiableProcesses);

            foreach (CompatibilityProcessInfo process in newProcesses)
            {
                _toastService?.ShowNewProcess(process);
            }
        }
        catch
        {
            // Toast monitoring is best-effort and must not terminate the tray app.
        }
        finally
        {
            _isNotificationRefreshRunning = false;
        }
    }

    private async Task<MonitoringSnapshot> ReadMonitoringSnapshotAsync()
    {
        IReadOnlyList<CompatibilityProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        MonitoringSettings settings = await _settingsStore.GetAsync();
        IReadOnlyList<CompatibilityProcessInfo> visibleProcesses = ArchitectureProcessFilter.FilterVisibleProcesses(
            IgnoredProcessFilter.Filter(processes, ignoredNames),
            settings);
        IReadOnlyList<CompatibilityProcessInfo> notifiableProcesses = ArchitectureProcessFilter.FilterNotifiableProcesses(
            visibleProcesses,
            settings);

        return new MonitoringSnapshot(visibleProcesses, notifiableProcesses);
    }
}

internal sealed record MonitoringSnapshot(
    IReadOnlyList<CompatibilityProcessInfo> VisibleProcesses,
    IReadOnlyList<CompatibilityProcessInfo> NotifiableProcesses);
