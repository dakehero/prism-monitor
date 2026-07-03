using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NativeGuard.Core.Processes;
using NativeGuard.Core.Ui;
using Windows.Graphics;

namespace NativeGuard_App;

public sealed partial class MainWindow : Window
{
    private readonly NonNativeProcessService _processService;
    private readonly SizeInt32 _windowSize = new(640, 420);

    public ObservableCollection<ProcessRow> Rows { get; } = [];

    public MainWindow(NonNativeProcessService processService)
    {
        _processService = processService;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(_windowSize);
    }

    public void MoveNear(ScreenRect trayIconRect)
    {
        DisplayArea displayArea = DisplayArea.GetFromPoint(
            new PointInt32(
                trayIconRect.X + trayIconRect.Width / 2,
                trayIconRect.Y + trayIconRect.Height / 2),
            DisplayAreaFallback.Primary);

        RectInt32 workArea = displayArea.WorkArea;
        ScreenPoint target = PopupPlacementCalculator.Calculate(
            trayIconRect,
            new ScreenSize(_windowSize.Width, _windowSize.Height),
            new ScreenRect(workArea.X, workArea.Y, workArea.Width, workArea.Height));

        AppWindow.Move(new PointInt32(target.X, target.Y));
    }

    public async Task RefreshAsync()
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

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
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
