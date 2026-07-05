using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Settings;
using PrismMonitor.Core.Ui;
using PrismMonitor.App.Notifications;
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
    private readonly CompatibilityProcessNotifier _processNotifier = new();
    private readonly TrayWindowLifetime _windowLifetime = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherTimer _notificationTimer = new();
    private MainWindow? _window;
    private ShellTrayIcon? _trayIcon;
    private CompatibilityProcessToastService? _toastService;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        EnsureMainWindow();
        AppInstance.GetCurrent().Activated += CurrentInstance_Activated;

        _trayIcon = new ShellTrayIcon(
            OpenProcessWindow,
            ExitApplication,
            GetTrayStatusAsync);

        _toastService = new CompatibilityProcessToastService(_ignoredProcessStore);
        _toastService.Register();
        _notificationTimer.Interval = TimeSpan.FromSeconds(1);
        _notificationTimer.Tick += NotificationTimer_Tick;
        _ = _dispatcherQueue.TryEnqueue(() => NotificationTimer_Tick(null, new object()));
        _notificationTimer.Start();
    }

    private void OpenProcessWindow()
    {
        MainWindow window = EnsureMainWindow();

        window.ShowMainWindow();
        window.Activate();
        ScheduleRefreshProcessWindow(window);
    }

    private void ExitApplication()
    {
        _windowLifetime.RequestExitClose();
        _notificationTimer.Stop();
        _window?.CloseForExit();
        _trayIcon?.Dispose();
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

        _window = new MainWindow(_processService, _ignoredProcessStore, _settingsStore);
        _windowLifetime.MarkWindowCreated();
        _window.HiddenToTray += (_, _) => _windowLifetime.MarkHiddenToTray();
        _window.Closed += (_, _) =>
        {
            _windowLifetime.MarkWindowClosed();
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
        _ = _dispatcherQueue.TryEnqueue(OpenProcessWindow);
    }

    private async void NotificationTimer_Tick(object? sender, object e)
    {
        try
        {
            IReadOnlyList<CompatibilityProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
            IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
            MonitoringSettings settings = await _settingsStore.GetAsync();
            IReadOnlyList<CompatibilityProcessInfo> visibleProcesses = ArchitectureProcessFilter.FilterVisibleProcesses(
                IgnoredProcessFilter.Filter(processes, ignoredNames),
                settings);
            _trayIcon?.UpdateStatus(CreateTrayStatus(visibleProcesses));

            IReadOnlyList<CompatibilityProcessInfo> notifiableProcesses = ArchitectureProcessFilter.FilterNotifiableProcesses(
                visibleProcesses,
                settings);
            IReadOnlyList<CompatibilityProcessInfo> newProcesses = _processNotifier.CaptureNewProcesses(notifiableProcesses);

            foreach (CompatibilityProcessInfo process in newProcesses)
            {
                _toastService?.ShowNewProcess(process);
            }
        }
        catch
        {
            // Toast monitoring is best-effort and must not terminate the tray app.
        }
    }
}
