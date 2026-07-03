using Microsoft.UI.Xaml;
using NativeGuard.Core.Processes;
using NativeGuard.Core.Ui;
using NativeGuard_App.Processes;
using NativeGuard_App.Tray;

namespace NativeGuard_App;

public partial class App : Application
{
    private readonly NonNativeProcessService _processService = new(new Win32ProcessInfoProvider());
    private MainWindow? _window;
    private ShellTrayIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _trayIcon = new ShellTrayIcon(
            OpenProcessWindow,
            ExitApplication,
            GetTooltipAsync);
    }

    private void OpenProcessWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow(_processService);
            _window.Closed += (_, _) => _window = null;
        }

        ShellTrayIcon? trayIcon = _trayIcon;

        _window.Activate();
        if (trayIcon is not null && trayIcon.TryGetIconRect(out ScreenRect trayIconRect))
        {
            _window.MoveNear(trayIconRect);
        }

        _ = _window.RefreshAsync();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        Exit();
    }

    private async Task<string> GetTooltipAsync()
    {
        IReadOnlyList<NonNativeProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
        return TrayTooltipFormatter.FormatTopProcesses(processes, 5);
    }
}
