using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using PrismMonitor.Core.History;
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
    private readonly LaunchHistoryStore _launchHistoryStore;
    private readonly ProcessIconProvider _iconProvider = new();
    private readonly ProcessTerminator _processTerminator = new();
    private readonly bool _isElevated;
    private readonly Func<bool> _relaunchAsAdministrator;
    private readonly DispatcherTimer _refreshTimer = new();
    // AppWindow uses window pixels; on the current 200% display scale this is about 818 x 488 XAML effective pixels.
    private readonly SizeInt32 _windowSize = new(1636, 975);
    private readonly HashSet<string> _cachedIgnoredNames = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<LaunchHistorySummary> _cachedHistorySummaries = [];
    private bool _isRefreshing;
    private bool _isRefreshingHistory;
    private bool _refreshHistoryAgain;
    private bool _allowClose;
    private bool _isLoadingSettings;

    public ObservableCollection<ProcessRow> Rows { get; } = [];
    public ObservableCollection<HistoryRow> HistoryRows { get; } = [];
    public ObservableCollection<FilterRow> FilterRows { get; } = [];
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
        MonitoringSettingsStore settingsStore,
        LaunchHistoryStore launchHistoryStore,
        bool isElevated,
        Func<bool> relaunchAsAdministrator)
    {
        _processService = processService;
        _ignoredProcessStore = ignoredProcessStore;
        _settingsStore = settingsStore;
        _launchHistoryStore = launchHistoryStore;
        _isElevated = isElevated;
        _relaunchAsAdministrator = relaunchAsAdministrator;
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
            await RefreshIgnoredCacheAsync();
            IReadOnlyList<CompatibilityProcessInfo> processes = await _processService.GetCurrentProcessesAsync();
            MonitoringSettings settings = await _settingsStore.GetAsync();
            IReadOnlyList<CompatibilityProcessInfo> visibleProcesses = ArchitectureProcessFilter.FilterVisibleProcesses(
                IgnoredProcessFilter.Filter(processes, _cachedIgnoredNames),
                settings);
            await ApplyProcessSnapshotAsync(visibleProcesses);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public async Task RefreshHistoryAsync()
    {
        if (_isRefreshingHistory)
        {
            _refreshHistoryAgain = true;
            return;
        }

        _isRefreshingHistory = true;
        try
        {
            do
            {
                _refreshHistoryAgain = false;
                await RefreshIgnoredCacheAsync();
                HashSet<string> ignoredNamesSnapshot = _cachedIgnoredNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                _cachedHistorySummaries = await _launchHistoryStore.GetSummaryAsync(
                    DateTimeOffset.UtcNow,
                    ignoredNamesSnapshot);
                ApplyCachedHistoryFilter();
            }
            while (_refreshHistoryAgain);
        }
        finally
        {
            _isRefreshingHistory = false;
        }
    }

    public async Task ShowProcessesAndFocusAsync(
        int processId,
        IReadOnlyList<CompatibilityProcessInfo>? processSnapshot = null)
    {
        SelectNavigationItem("Processes");
        ShowPage("Processes");

        if (processSnapshot is not null)
        {
            await ApplyProcessSnapshotAsync(processSnapshot);
        }
        else
        {
            await RefreshAsync();
        }

        ProcessRow? row = Rows.FirstOrDefault(process => process.ProcessId == processId);
        if (row is not null)
        {
            ProcessListView.SelectedItem = row;
            ProcessListView.ScrollIntoView(row);
        }
    }

    public async Task ShowHistoryForProcessAsync(string processName)
    {
        SelectNavigationItem("History");
        ShowPage("History");
        HistoryArchitectureComboBox.SelectedIndex = 0;
        HistoryIgnoredStateComboBox.SelectedIndex = 0;
        HistorySearchTextBox.Text = processName;
        await RefreshHistoryAsync();
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

    private void HistoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!AreHistoryFilterControlsReady())
        {
            return;
        }

        ApplyCachedHistoryFilter();
    }

    private async void RefreshHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshHistoryAsync();
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        ClearHistoryButton.IsEnabled = false;
        HistoryDataStatusTextBlock.Text = "Clearing launch history...";

        try
        {
            await _launchHistoryStore.ClearAsync();
            await RefreshHistoryAsync();
            HistoryDataStatusTextBlock.Text = "Launch history cleared.";
        }
        catch (Exception)
        {
            HistoryDataStatusTextBlock.Text = "Could not clear launch history.";
        }
        finally
        {
            ClearHistoryButton.IsEnabled = true;
        }
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        LoadElevationState();
        await ReloadIgnoredNamesAsync();
        await LoadSettingsControlsAsync();
    }

    private void RelaunchAsAdministratorButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_relaunchAsAdministrator())
        {
            ElevationStatusTextBlock.Text = "Administrator relaunch was cancelled or could not be started.";
        }
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

    private async void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string selectedTag = args.IsSettingsSelected
            ? "Settings"
            : args.SelectedItemContainer?.Tag as string ?? "Processes";

        ShowPage(selectedTag);

        if (string.Equals(selectedTag, "History", StringComparison.Ordinal))
        {
            await RefreshHistoryAsync();
        }
        else if (string.Equals(selectedTag, "Filters", StringComparison.Ordinal))
        {
            await ReloadIgnoredNamesAsync();
        }
    }

    private void ShowPage(string selectedTag)
    {
        ProcessesPage.Visibility = string.Equals(selectedTag, "Processes", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
        FiltersPage.Visibility = string.Equals(selectedTag, "Filters", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
        HistoryPage.Visibility = string.Equals(selectedTag, "History", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
        SettingsPage.Visibility = string.Equals(selectedTag, "Settings", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SelectNavigationItem(string tag)
    {
        foreach (object item in RootNavigationView.MenuItems)
        {
            if (item is NavigationViewItem navigationItem
                && string.Equals(navigationItem.Tag as string, tag, StringComparison.Ordinal))
            {
                RootNavigationView.SelectedItem = navigationItem;
                return;
            }
        }

        if (string.Equals(tag, "Settings", StringComparison.Ordinal))
        {
            RootNavigationView.SelectedItem = RootNavigationView.SettingsItem;
        }
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        await RefreshAsync();
    }

    private async Task ReloadIgnoredNamesAsync()
    {
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        await ApplyIgnoredNamesAsync(ignoredNames);
    }

    private async Task RefreshIgnoredCacheAsync()
    {
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        ApplyIgnoredCache(ignoredNames);
    }

    private Task ApplyIgnoredNamesAsync(IReadOnlyList<string> ignoredNames)
    {
        ApplyIgnoredCache(ignoredNames);
        ApplyFilterRows(ignoredNames);
        return Task.CompletedTask;
    }

    private void ApplyIgnoredCache(IReadOnlyList<string> ignoredNames)
    {
        _cachedIgnoredNames.Clear();
        foreach (string ignoredName in ignoredNames)
        {
            _cachedIgnoredNames.Add(ignoredName);
        }
    }

    private void ApplyFilterRows(IReadOnlyList<string> ignoredNames)
    {
        HashSet<string> nextIgnoredNames = ignoredNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (FilterRow rowToRemove in FilterRows.Where(row => !nextIgnoredNames.Contains(row.Name)).ToList())
        {
            FilterRows.Remove(rowToRemove);
        }

        foreach (string ignoredName in ignoredNames)
        {
            if (IndexOfFilterRow(ignoredName) < 0)
            {
                FilterRows.Add(new FilterRow(ignoredName));
            }
        }

        MoveFilterRowsIntoSortedOrder(ignoredNames);
    }

    private void MoveFilterRowsIntoSortedOrder(IReadOnlyList<string> ignoredNames)
    {
        for (int targetIndex = 0; targetIndex < ignoredNames.Count; targetIndex++)
        {
            string ignoredName = ignoredNames[targetIndex];
            int currentIndex = IndexOfFilterRow(ignoredName);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                FilterRows.Move(currentIndex, targetIndex);
            }
        }
    }

    private int IndexOfFilterRow(string ignoredName)
    {
        for (int index = 0; index < FilterRows.Count; index++)
        {
            if (string.Equals(FilterRows[index].Name, ignoredName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
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

    private void LoadElevationState()
    {
        if (_isElevated)
        {
            ElevationStatusTextBlock.Text = "Running as administrator. Full process visibility is available, but system notifications are unavailable while elevated.";
            RelaunchAsAdministratorButton.IsEnabled = false;
            return;
        }

        ElevationStatusTextBlock.Text = "Running with standard user permissions. System notifications are available.";
        RelaunchAsAdministratorButton.IsEnabled = true;
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

    private void ApplyCachedHistoryFilter()
    {
        IReadOnlyList<LaunchHistorySummary> filteredSummaries = LaunchHistoryFilter.Apply(
            _cachedHistorySummaries,
            GetHistoryQueryFromControls());

        ApplyHistorySnapshot(filteredSummaries);
    }

    private void ApplyHistorySnapshot(IReadOnlyList<LaunchHistorySummary> summaries)
    {
        Dictionary<string, HistoryRow> rowsByKey = HistoryRows.ToDictionary(
            row => GetHistoryKey(row.ProcessName, row.Architecture, row.ExecutablePath),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> summaryKeys = summaries
            .Select(summary => GetHistoryKey(summary.ProcessName, summary.Architecture, summary.LastExecutablePath ?? string.Empty))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (HistoryRow removedRow in HistoryRows
            .Where(row => !summaryKeys.Contains(GetHistoryKey(row.ProcessName, row.Architecture, row.ExecutablePath)))
            .ToList())
        {
            HistoryRows.Remove(removedRow);
        }

        foreach (LaunchHistorySummary summary in summaries)
        {
            string executablePath = summary.LastExecutablePath ?? string.Empty;
            string key = GetHistoryKey(summary.ProcessName, summary.Architecture, executablePath);
            if (rowsByKey.TryGetValue(key, out HistoryRow? row))
            {
                row.Update(
                    summary.ProcessName,
                    summary.Architecture,
                    summary.LaunchCount,
                    FormatHistoryTimestamp(summary.FirstSeenAt),
                    FormatHistoryTimestamp(summary.LastSeenAt),
                    executablePath,
                    summary.IsIgnored ? "Ignored" : string.Empty);
            }
            else
            {
                HistoryRows.Add(new HistoryRow(
                    summary.ProcessName,
                    summary.Architecture,
                    summary.LaunchCount,
                    FormatHistoryTimestamp(summary.FirstSeenAt),
                    FormatHistoryTimestamp(summary.LastSeenAt),
                    executablePath,
                    summary.IsIgnored ? "Ignored" : string.Empty));
            }
        }

        MoveHistoryRowsIntoSortedOrder(summaries);
    }

    private void MoveHistoryRowsIntoSortedOrder(IReadOnlyList<LaunchHistorySummary> summaries)
    {
        for (int targetIndex = 0; targetIndex < summaries.Count; targetIndex++)
        {
            LaunchHistorySummary summary = summaries[targetIndex];
            int currentIndex = IndexOfHistoryRow(
                summary.ProcessName,
                summary.Architecture,
                summary.LastExecutablePath ?? string.Empty);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                HistoryRows.Move(currentIndex, targetIndex);
            }
        }
    }

    private int IndexOfHistoryRow(string processName, string architecture, string executablePath)
    {
        string key = GetHistoryKey(processName, architecture, executablePath);
        for (int index = 0; index < HistoryRows.Count; index++)
        {
            HistoryRow row = HistoryRows[index];
            if (string.Equals(
                GetHistoryKey(row.ProcessName, row.Architecture, row.ExecutablePath),
                key,
                StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetHistoryKey(string processName, string architecture, string executablePath)
    {
        return string.Concat(processName, '\u001f', architecture, '\u001f', executablePath);
    }

    private LaunchHistoryQuery GetHistoryQueryFromControls()
    {
        return new LaunchHistoryQuery(
            HistorySearchTextBox.Text,
            GetSelectedComboBoxText(HistoryArchitectureComboBox) is "All" ? null : GetSelectedComboBoxText(HistoryArchitectureComboBox),
            GetHistoryIgnoredState());
    }

    private bool AreHistoryFilterControlsReady()
    {
        return HistorySearchTextBox is not null
            && HistoryArchitectureComboBox is not null
            && HistoryIgnoredStateComboBox is not null;
    }

    private LaunchHistoryIgnoredState GetHistoryIgnoredState()
    {
        return GetSelectedComboBoxText(HistoryIgnoredStateComboBox) switch
        {
            "Ignored" => LaunchHistoryIgnoredState.IgnoredOnly,
            "Not ignored" => LaunchHistoryIgnoredState.NotIgnoredOnly,
            _ => LaunchHistoryIgnoredState.All
        };
    }

    private static string? GetSelectedComboBoxText(ComboBox? comboBox)
    {
        if (comboBox is null)
        {
            return null;
        }

        return comboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString()
            : comboBox.SelectedItem?.ToString();
    }

    private static string FormatHistoryTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("g");
    }
}
