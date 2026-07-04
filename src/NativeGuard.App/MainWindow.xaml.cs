using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NativeGuard.Core.Processes;
using Windows.Graphics;

namespace NativeGuard_App;

public sealed partial class MainWindow : Window
{
    private readonly NonNativeProcessService _processService;
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly SizeInt32 _windowSize = new(860, 560);
    private bool _isRefreshing;
    private bool _allowClose;

    public ObservableCollection<ProcessRow> Rows { get; } = [];

    public event EventHandler? HiddenToTray;

    public MainWindow(NonNativeProcessService processService)
    {
        _processService = processService;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(_windowSize);

        _refreshTimer.Interval = TimeSpan.FromSeconds(3);
        _refreshTimer.Tick += RefreshTimer_Tick;
        AppWindow.Closing += AppWindow_Closing;
        Closed += (_, _) => _refreshTimer.Stop();
    }

    public void ShowMainWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        AppWindow.Show();
        _refreshTimer.Start();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    public async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            IReadOnlyList<NonNativeProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
            Rows.Clear();

            foreach (NonNativeProcessInfo process in processes)
            {
                Rows.Add(new ProcessRow(
                    process.Name,
                    process.ProcessId,
                    process.Architecture,
                    CpuTimeFormatter.Format(process.CpuTime)));
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        await RefreshAsync();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        _refreshTimer.Stop();
        sender.Hide();
        HiddenToTray?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class ProcessRow
{
    public ProcessRow(string name, int processId, string architecture, string cpuTime)
    {
        Name = name;
        ProcessId = processId;
        Architecture = architecture;
        CpuTime = cpuTime;
    }

    public string Name { get; set; }

    public int ProcessId { get; set; }

    public string Architecture { get; set; }

    public string CpuTime { get; set; }
}
