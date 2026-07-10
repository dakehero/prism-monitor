using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using PrismMonitor.Core.History;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;
using PrismMonitor.Core.Settings;
using PrismMonitor.Core.Ui;
using PrismMonitor.App.Processes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace PrismMonitor.App;

public sealed partial class MainWindow : Window
{
    private readonly IgnoredProcessStore _ignoredProcessStore;
    private readonly MonitoringSettingsStore _settingsStore;
    private readonly LaunchHistoryStore _launchHistoryStore;
    private readonly ProcessIconProvider _iconProvider = new();
    private readonly ProcessTerminator _processTerminator = new();
    private readonly LatestAsyncWorkGate<IReadOnlyList<CompatibilityProcessInfo>> _processSnapshotGate = new();
    // AppWindow uses window pixels; on the current 200% display scale this is about 818 x 488 XAML effective pixels.
    private readonly SizeInt32 _windowSize = new(1636, 975);
    private IReadOnlyList<LaunchHistorySummary> _cachedHistorySummaries = [];
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

    public event EventHandler? RefreshRequested;

    public event EventHandler? ConfigurationChanged;

    public MainWindow(
        IgnoredProcessStore ignoredProcessStore,
        MonitoringSettingsStore settingsStore,
        LaunchHistoryStore launchHistoryStore)
    {
        _ignoredProcessStore = ignoredProcessStore;
        _settingsStore = settingsStore;
        _launchHistoryStore = launchHistoryStore;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(_windowSize);
        UpdateProcessStatusText();
        UpdateHistoryStatusText();

        AppWindow.Closing += AppWindow_Closing;
    }

    public void ShowMainWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        AppWindow.Show();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    public Task ApplyMonitoringSnapshotAsync(MonitoringSnapshot snapshot)
    {
        return ApplyProcessSnapshotSerializedAsync(snapshot.Processes);
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
                IReadOnlyList<AppIdentityRule> rules = await _ignoredProcessStore.GetRulesAsync();
                _cachedHistorySummaries = await _launchHistoryStore.GetSummaryWithRulesAsync(
                    DateTimeOffset.UtcNow,
                    rules);
                await ApplyCachedHistoryFilterAsync();
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
            await ApplyProcessSnapshotSerializedAsync(processSnapshot);
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

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TerminateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProcessRow row })
        {
            return;
        }

        ProcessTerminationResult result = _processTerminator.Terminate(row.ProcessId);
        if (!result.Succeeded)
        {
            return;
        }

        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void IgnoreProcessMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProcessRow row })
        {
            return;
        }

        await _ignoredProcessStore.AddAsync(row.Name);
        await ReloadIgnoredNamesAsync();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void AddIgnoredButton_Click(object sender, RoutedEventArgs e)
    {
        await _ignoredProcessStore.AddAsync(IgnoreNameTextBox.Text);
        IgnoreNameTextBox.Text = string.Empty;
        await ReloadIgnoredNamesAsync();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void HistoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!AreHistoryFilterControlsReady())
        {
            return;
        }

        await ApplyCachedHistoryFilterAsync();
    }

    private async void RefreshHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshHistoryAsync();
    }

    private async void CopyValueButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is null)
        {
            return;
        }

        string text = Convert.ToString(button.Tag, CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);

        object originalContent = button.Content;
        button.Content = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1));

        if (button.Content is string currentContent
            && string.Equals(currentContent, "Copied", StringComparison.Ordinal))
        {
            button.Content = originalContent;
        }
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
        await ReloadIgnoredNamesAsync();
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
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
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

    private async Task ReloadIgnoredNamesAsync()
    {
        IReadOnlyList<string> ignoredNames = await _ignoredProcessStore.GetIgnoredNamesAsync();
        await ApplyIgnoredNamesAsync(ignoredNames);
    }

    private Task ApplyIgnoredNamesAsync(IReadOnlyList<string> ignoredNames)
    {
        ApplyFilterRows(ignoredNames);
        return Task.CompletedTask;
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
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
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
                    process.ExecutablePath ?? string.Empty,
                    await _iconProvider.GetIconAsync(
                        process.Name,
                        process.ExecutablePath,
                        process.IconCacheKey),
                    process.HasLimitedDetails);
            }
            else
            {
                Rows.Add(new ProcessRow(
                    process.Name,
                    process.ProcessId,
                    process.Architecture,
                    CpuTimeFormatter.Format(process.CpuTime),
                    process.ExecutablePath ?? string.Empty,
                    await _iconProvider.GetIconAsync(
                        process.Name,
                        process.ExecutablePath,
                        process.IconCacheKey),
                    process.HasLimitedDetails));
            }
        }

        MoveRowsIntoSortedOrder(diff.SortedRows);
        UpdateProcessStatusText();
    }

    private Task ApplyProcessSnapshotSerializedAsync(IReadOnlyList<CompatibilityProcessInfo> processes)
    {
        return _processSnapshotGate.RunAsync(processes, ApplyProcessSnapshotAsync);
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

    private async Task ApplyCachedHistoryFilterAsync()
    {
        IReadOnlyList<LaunchHistorySummary> filteredSummaries = LaunchHistoryFilter.Apply(
            _cachedHistorySummaries,
            GetHistoryQueryFromControls());

        await ApplyHistorySnapshotAsync(filteredSummaries);
    }

    private async Task ApplyHistorySnapshotAsync(IReadOnlyList<LaunchHistorySummary> summaries)
    {
        Dictionary<string, HistoryRow> rowsByKey = HistoryRows.ToDictionary(
            GetHistoryKey,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> summaryKeys = summaries
            .Select(GetHistoryKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (HistoryRow removedRow in HistoryRows
            .Where(row => !summaryKeys.Contains(GetHistoryKey(row)))
            .ToList())
        {
            HistoryRows.Remove(removedRow);
        }

        foreach (LaunchHistorySummary summary in summaries)
        {
            string executablePath = summary.LastExecutablePath ?? string.Empty;
            string packageIdentity = summary.PackageIdentity ?? string.Empty;
            string publisherIdentity = summary.PublisherIdentity ?? string.Empty;
            string key = GetHistoryKey(summary);
            if (rowsByKey.TryGetValue(key, out HistoryRow? row))
            {
                row.Update(
                    summary.ProcessName,
                    summary.Architecture,
                    summary.LaunchCount,
                    FormatHistoryTimestamp(summary.FirstSeenAt),
                    FormatHistoryTimestamp(summary.LastSeenAt),
                    summary.LastProcessId,
                    executablePath,
                    packageIdentity,
                    publisherIdentity,
                    summary.IsIgnored ? "Ignored" : string.Empty,
                    await _iconProvider.GetIconAsync(summary.ProcessName, summary.LastExecutablePath));
            }
            else
            {
                HistoryRows.Add(new HistoryRow(
                    summary.ProcessName,
                    summary.Architecture,
                    summary.LaunchCount,
                    FormatHistoryTimestamp(summary.FirstSeenAt),
                    FormatHistoryTimestamp(summary.LastSeenAt),
                    summary.LastProcessId,
                    executablePath,
                    packageIdentity,
                    publisherIdentity,
                    summary.IsIgnored ? "Ignored" : string.Empty,
                    await _iconProvider.GetIconAsync(summary.ProcessName, summary.LastExecutablePath)));
            }
        }

        MoveHistoryRowsIntoSortedOrder(summaries);
        UpdateHistoryStatusText();
    }

    private void UpdateProcessStatusText()
    {
        int count = Rows.Count;
        ProcessStatusTextBlock.Text = count == 1
            ? "1 active process"
            : string.Concat(count.ToString(), " active processes");
    }

    private void UpdateHistoryStatusText()
    {
        int visibleCount = HistoryRows.Count;
        int totalCount = _cachedHistorySummaries.Count;
        if (visibleCount == totalCount)
        {
            HistoryStatusTextBlock.Text = visibleCount == 1
                ? "1 history entry"
                : string.Concat(visibleCount.ToString(), " history entries");
            return;
        }

        HistoryStatusTextBlock.Text = string.Concat(
            visibleCount.ToString(),
            " of ",
            totalCount.ToString(),
            " history entries shown");
    }

    private void MoveHistoryRowsIntoSortedOrder(IReadOnlyList<LaunchHistorySummary> summaries)
    {
        for (int targetIndex = 0; targetIndex < summaries.Count; targetIndex++)
        {
            LaunchHistorySummary summary = summaries[targetIndex];
            int currentIndex = IndexOfHistoryRow(summary);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                HistoryRows.Move(currentIndex, targetIndex);
            }
        }
    }

    private int IndexOfHistoryRow(LaunchHistorySummary summary)
    {
        string key = GetHistoryKey(summary);
        for (int index = 0; index < HistoryRows.Count; index++)
        {
            HistoryRow row = HistoryRows[index];
            if (string.Equals(
                GetHistoryKey(row),
                key,
                StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetHistoryKey(HistoryRow row)
    {
        return GetHistoryKey(
            row.ProcessName,
            row.Architecture,
            row.ExecutablePath,
            row.PackageIdentity,
            row.PublisherIdentity);
    }

    private static string GetHistoryKey(LaunchHistorySummary summary)
    {
        return GetHistoryKey(
            summary.ProcessName,
            summary.Architecture,
            summary.LastExecutablePath ?? string.Empty,
            summary.PackageIdentity ?? string.Empty,
            summary.PublisherIdentity ?? string.Empty);
    }

    private static string GetHistoryKey(
        string processName,
        string architecture,
        string executablePath,
        string packageIdentity,
        string publisherIdentity)
    {
        return string.Concat(
            processName,
            '\u001f',
            architecture,
            '\u001f',
            executablePath,
            '\u001f',
            packageIdentity,
            '\u001f',
            publisherIdentity);
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

        if (comboBox.SelectedValue is string selectedValue && !string.IsNullOrWhiteSpace(selectedValue))
        {
            return selectedValue;
        }

        if (comboBox.SelectedItem is ComboBoxItem item)
        {
            if (item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }

            return item.Content switch
            {
                string text => text,
                ContentControl { Content: string text } => text,
                _ => item.Content?.ToString()
            };
        }

        if (comboBox.SelectedItem is ContentControl { Tag: string selectedItemTag }
            && !string.IsNullOrWhiteSpace(selectedItemTag))
        {
            return selectedItemTag;
        }

        if (comboBox.SelectedItem is ContentControl { Content: string selectedItemText })
        {
            return selectedItemText;
        }

        return comboBox.SelectedItem?.ToString();
    }

    private static string FormatHistoryTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("g");
    }
}
