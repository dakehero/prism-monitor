using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Settings;
using PrismMonitor.App.Processes;
using Windows.Graphics;

namespace PrismMonitor.App;

public sealed partial class MainWindow : Window
{
    private readonly CompatibilityProcessService _processService;
    private readonly IgnoredProcessStore _ignoredProcessStore;
    private readonly MonitoringSettingsStore _settingsStore;
    private readonly ProcessIconProvider _iconProvider = new();
    private readonly ProcessTerminator _processTerminator = new();
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly SizeInt32 _windowSize = new(1040, 620);
    private bool _isRefreshing;
    private bool _allowClose;
    private bool _isLoadingSettings;

    public ObservableCollection<ProcessRow> Rows { get; } = [];
    public ObservableCollection<string> IgnoredNames { get; } = [];
    public ObservableCollection<NotificationLevelOption> NotificationLevelOptions { get; } =
    [
        new(NotificationLevel.X86Only, "x86 only"),
        new(NotificationLevel.X86AndX64, "x86 + x64"),
        new(NotificationLevel.X86X64AndArm64Ec, "x86 + x64 + ARM64EC")
    ];

    public event EventHandler? HiddenToTray;

    public MainWindow(
        CompatibilityProcessService processService,
        IgnoredProcessStore ignoredProcessStore,
        MonitoringSettingsStore settingsStore)
    {
        _processService = processService;
        _ignoredProcessStore = ignoredProcessStore;
        _settingsStore = settingsStore;
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
            await ReloadIgnoredNamesAsync();
            IReadOnlyList<CompatibilityProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
            MonitoringSettings settings = await _settingsStore.GetAsync();
            IReadOnlyList<CompatibilityProcessInfo> visibleProcesses = ArchitectureProcessFilter.FilterVisibleProcesses(
                IgnoredProcessFilter.Filter(processes, IgnoredNames),
                settings);
            await ApplyProcessSnapshotAsync(visibleProcesses);
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

    private async void TerminateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: ProcessRow row })
        {
            return;
        }

        ProcessTerminationResult result = _processTerminator.Terminate(row.ProcessId);
        if (!result.Succeeded)
        {
            return;
        }

        await RefreshAsync();
    }

    private async void IgnoreProcessMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: ProcessRow row })
        {
            return;
        }

        await _ignoredProcessStore.AddAsync(row.Name);
        await ReloadIgnoredNamesAsync();
        await RefreshAsync();
    }

    private async void AddIgnoredButton_Click(object sender, RoutedEventArgs e)
    {
        await _ignoredProcessStore.AddAsync(IgnoreNameTextBox.Text);
        IgnoreNameTextBox.Text = string.Empty;
        await ReloadIgnoredNamesAsync();
        await RefreshAsync();
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsControlsAsync();
    }

    private async void IncludeArm64EcToggle_Toggled(object sender, RoutedEventArgs e)
    {
        await SaveSettingsFromControlsAsync();
    }

    private async void NotificationLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveSettingsFromControlsAsync();
    }

    private async void RemoveIgnoredButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string ignoredName })
        {
            return;
        }

        await _ignoredProcessStore.RemoveAsync(ignoredName);
        await ReloadIgnoredNamesAsync();
        await RefreshAsync();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        await RefreshAsync();
    }

    private async Task ReloadIgnoredNamesAsync()
    {
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        IgnoredNames.Clear();
        foreach (string ignoredName in ignoredNames)
        {
            IgnoredNames.Add(ignoredName);
        }
    }

    private async Task LoadSettingsControlsAsync()
    {
        _isLoadingSettings = true;
        try
        {
            MonitoringSettings settings = await _settingsStore.GetAsync();
            IncludeArm64EcToggle.IsOn = settings.IncludeArm64EcProcesses;
            NotificationLevelComboBox.SelectedItem = NotificationLevelOptions.FirstOrDefault(
                option => option.Level == settings.NotificationLevel);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async Task SaveSettingsFromControlsAsync()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        NotificationLevel level = NotificationLevelComboBox.SelectedItem is NotificationLevelOption option
            ? option.Level
            : NotificationLevel.X86X64AndArm64Ec;

        await _settingsStore.SaveAsync(new MonitoringSettings(IncludeArm64EcToggle.IsOn, level));
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

    private async Task ApplyProcessSnapshotAsync(IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        Dictionary<int, ProcessRow> rowsByProcessId = Rows.ToDictionary(row => row.ProcessId);
        ProcessListDiff diff = ProcessListDiffer.Diff(rowsByProcessId.Keys, processes);

        foreach (int removedProcessId in diff.RemovedProcessIds)
        {
            if (rowsByProcessId.TryGetValue(removedProcessId, out ProcessRow? removedRow))
            {
                Rows.Remove(removedRow);
            }
        }

        foreach (CompatibilityProcessInfo process in diff.SortedRows)
        {
            if (rowsByProcessId.TryGetValue(process.ProcessId, out ProcessRow? row))
            {
                row.Update(
                    process.Name,
                    process.Architecture,
                    CpuTimeFormatter.Format(process.CpuTime),
                    await _iconProvider.GetIconAsync(process.Name, process.ExecutablePath));
            }
            else
            {
                Rows.Add(new ProcessRow(
                    process.Name,
                    process.ProcessId,
                    process.Architecture,
                    CpuTimeFormatter.Format(process.CpuTime),
                    await _iconProvider.GetIconAsync(process.Name, process.ExecutablePath)));
            }
        }

        MoveRowsIntoSortedOrder(diff.SortedRows);
    }

    private void MoveRowsIntoSortedOrder(IReadOnlyList<CompatibilityProcessInfo> sortedRows)
    {
        for (int targetIndex = 0; targetIndex < sortedRows.Count; targetIndex++)
        {
            int processId = sortedRows[targetIndex].ProcessId;
            int currentIndex = IndexOfProcessRow(processId);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                Rows.Move(currentIndex, targetIndex);
            }
        }
    }

    private int IndexOfProcessRow(int processId)
    {
        for (int index = 0; index < Rows.Count; index++)
        {
            if (Rows[index].ProcessId == processId)
            {
                return index;
            }
        }

        return -1;
    }
}

public sealed class ProcessRow : INotifyPropertyChanged
{
    private string _name;
    private string _architecture;
    private string _cpuTime;
    private ImageSource _icon;

    public ProcessRow(string name, int processId, string architecture, string cpuTime, ImageSource icon)
    {
        _name = name;
        ProcessId = processId;
        _architecture = architecture;
        _cpuTime = cpuTime;
        _icon = icon;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public int ProcessId { get; set; }

    public string Architecture
    {
        get => _architecture;
        private set => SetProperty(ref _architecture, value);
    }

    public string CpuTime
    {
        get => _cpuTime;
        private set => SetProperty(ref _cpuTime, value);
    }

    public ImageSource Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public void Update(string name, string architecture, string cpuTime, ImageSource icon)
    {
        Name = name;
        Architecture = architecture;
        CpuTime = cpuTime;
        Icon = icon;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class NotificationLevelOption(NotificationLevel level, string displayName)
{
    public NotificationLevel Level { get; set; } = level;

    public string DisplayName { get; set; } = displayName;
}
