using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using NativeGuard.Core.Processes;

namespace NativeGuard_App;

public sealed partial class MainWindow : Window
{
    private readonly NonNativeProcessService _processService;

    public ObservableCollection<ProcessRow> Rows { get; } = [];

    public MainWindow(NonNativeProcessService processService)
    {
        _processService = processService;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 420));
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
