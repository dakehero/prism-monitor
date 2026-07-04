using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using NativeGuard.Core.Processes;
using NativeGuard.Core.Ui;
using NativeGuard_App.Processes;
using NativeGuard_App.Tray;

namespace NativeGuard_App;

public partial class App : Application
{
    private readonly NonNativeProcessService _processService = new(new Win32ProcessInfoProvider());
    private readonly TrayWindowLifetime _windowLifetime = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private MainWindow? _window;
    private ShellTrayIcon? _trayIcon;

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
            GetTooltipAsync);
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
        _window?.CloseForExit();
        _trayIcon?.Dispose();
        Exit();
    }

    private async Task<string> GetTooltipAsync()
    {
        IReadOnlyList<NonNativeProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
        return TrayTooltipFormatter.FormatTopProcesses(processes, 5);
    }

    private MainWindow EnsureMainWindow()
    {
        if (!_windowLifetime.NeedsWindow && _window is not null)
        {
            return _window;
        }

        _window = new MainWindow(_processService);
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
}
