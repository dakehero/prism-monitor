using Microsoft.UI.Xaml;
using NativeGuard.Core.Processes;
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

        _window.Activate();
        _window.ShowMainWindow();
        _ = RefreshProcessWindowAsync(_window);
    }

    private void ExitApplication()
    {
        _window?.CloseForExit();
        _trayIcon?.Dispose();
        Exit();
    }

    private async Task<string> GetTooltipAsync()
    {
        IReadOnlyList<NonNativeProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
        return TrayTooltipFormatter.FormatTopProcesses(processes, 5);
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
}
