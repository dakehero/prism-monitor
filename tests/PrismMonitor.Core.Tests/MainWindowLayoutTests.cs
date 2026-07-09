namespace PrismMonitor.Core.Tests;

[TestClass]
public sealed class MainWindowLayoutTests
{
    [TestMethod]
    public void MainWindowListsExposeVerticalScrollbars()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        StringAssert.Contains(xaml, "x:Name=\"ProcessListView\"");
        StringAssert.Contains(xaml, "x:Name=\"HistoryListView\"");
        StringAssert.Contains(xaml, "x:Name=\"RuleListView\"");
        Assert.IsGreaterThanOrEqualTo(
            CountOccurrences(xaml, "ScrollViewer.VerticalScrollBarVisibility=\"Auto\""),
            3,
            "Process, history, and rule lists should explicitly expose vertical scrollbars.");
    }

    [TestMethod]
    public void SettingsPageUsesScrollViewer()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        StringAssert.Contains(xaml, "x:Name=\"SettingsScrollViewer\"");
        StringAssert.Contains(xaml, "VerticalScrollBarVisibility=\"Auto\"");
        StringAssert.Contains(xaml, "HorizontalScrollBarVisibility=\"Disabled\"");

        int settingsPageIndex = xaml.IndexOf("x:Name=\"SettingsPage\"", StringComparison.Ordinal);
        int settingsScrollViewerIndex = xaml.IndexOf("x:Name=\"SettingsScrollViewer\"", settingsPageIndex, StringComparison.Ordinal);
        string settingsGridHeader = xaml[settingsPageIndex..settingsScrollViewerIndex];

        StringAssert.Contains(settingsGridHeader, "<RowDefinition Height=\"*\" />");
    }

    [TestMethod]
    public void HistoryFilterUsesHorizontalScrollViewer()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        StringAssert.Contains(xaml, "x:Name=\"HistoryFilterScrollViewer\"");
        StringAssert.Contains(xaml, "HorizontalScrollBarVisibility=\"Auto\"");
        StringAssert.Contains(xaml, "HorizontalScrollMode=\"Enabled\"");
        StringAssert.Contains(xaml, "VerticalScrollBarVisibility=\"Disabled\"");
    }

    [TestMethod]
    public void HistoryFilterUsesUnframedToolbar()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));
        string historyFilter = SliceBetween(xaml, "x:Name=\"HistoryFilterScrollViewer\"", "x:Name=\"HistoryListView\"");

        StringAssert.Contains(historyFilter, "<Grid");
        StringAssert.Contains(historyFilter, "x:Name=\"HistoryToolbar\"");
        Assert.IsFalse(
            historyFilter.Contains("<Border", StringComparison.Ordinal),
            "History filters should be an unframed toolbar, not another card nested above card rows.");
        Assert.IsFalse(
            historyFilter.Contains("CardBackgroundFillColorDefaultBrush", StringComparison.Ordinal)
                || historyFilter.Contains("CardStrokeColorDefaultBrush", StringComparison.Ordinal),
            "History filters should not reuse card styling reserved for list rows.");
    }

    [TestMethod]
    public void MainWindowListDetailsRowsUseLeftAlignedText()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        string processList = SliceBetween(xaml, "x:Name=\"ProcessListView\"", "x:Name=\"HistoryPage\"");
        string historyList = SliceBetween(xaml, "x:Name=\"HistoryListView\"", "x:Name=\"RulesPage\"");
        string rulesPage = SliceBetween(xaml, "x:Name=\"RulesPage\"", "x:Name=\"SettingsPage\"");
        string ruleList = SliceBetween(xaml, "x:Name=\"RuleListView\"", "x:Name=\"SettingsPage\"");

        Assert.IsFalse(
            processList.Contains("<ListView.Header>", StringComparison.Ordinal),
            "Processes should use list/details rows instead of a pseudo-table header.");
        Assert.IsFalse(
            historyList.Contains("<ListView.Header>", StringComparison.Ordinal),
            "History should use list/details rows instead of a pseudo-table header.");
        StringAssert.Contains(processList, "x:Name=\"ProcessSummaryLine\"");
        StringAssert.Contains(historyList, "x:Name=\"HistorySummaryLine\"");
        Assert.IsGreaterThanOrEqualTo(
            4,
            CountOccurrences(processList, "TextAlignment=\"Left\""),
            "Process list detail text should explicitly align text to the left.");
        Assert.IsGreaterThanOrEqualTo(
            6,
            CountOccurrences(historyList, "TextAlignment=\"Left\""),
            "History list detail text should explicitly align text to the left.");
        StringAssert.Contains(ruleList, "TextAlignment=\"Left\"");
        StringAssert.Contains(rulesPage, "x:Name=\"RulesHeading\"");
        StringAssert.Contains(rulesPage, "TextAlignment=\"Left\"");
        Assert.IsGreaterThanOrEqualTo(
            3,
            CountOccurrences(xaml, "<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />"),
            "Process, history, and rule list rows should stretch so left-aligned text starts at the column edge.");
    }

    [TestMethod]
    public void RulesPageUsesFirstClassRulePresentation()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));
        string rulesPage = SliceBetween(xaml, "x:Name=\"RulesPage\"", "x:Name=\"SettingsPage\"");

        StringAssert.Contains(xaml, "Content=\"Rules\"");
        StringAssert.Contains(rulesPage, "x:Name=\"RulesHeading\"");
        StringAssert.Contains(rulesPage, "Text=\"App rules\"");
        StringAssert.Contains(rulesPage, "x:Name=\"RuleListView\"");
        StringAssert.Contains(rulesPage, "Text=\"{x:Bind DisplayName, Mode=OneWay}\"");
        StringAssert.Contains(rulesPage, "Text=\"{x:Bind MatchSummary, Mode=OneWay}\"");
        StringAssert.Contains(rulesPage, "Text=\"{x:Bind TargetsText, Mode=OneWay}\"");
        StringAssert.Contains(rulesPage, "Click=\"RemoveRuleButton_Click\"");
        StringAssert.Contains(rulesPage, "<ColumnDefinition Width=\"*\" MinWidth=\"0\" />");
        StringAssert.Contains(rulesPage, "TextWrapping=\"WrapWholeWords\"");
        StringAssert.Contains(rulesPage, "VerticalAlignment=\"Center\"");
        Assert.IsFalse(
            xaml.Contains("x:Name=\"FiltersPage\"", StringComparison.Ordinal)
                || xaml.Contains("x:Name=\"FilterListView\"", StringComparison.Ordinal),
            "The legacy Filters page should become a first-class Rules page.");
    }

    [TestMethod]
    public void ProcessAndHistoryRowsUseExpandableCompactDetailsStructure()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        string processList = SliceBetween(xaml, "x:Name=\"ProcessListView\"", "x:Name=\"HistoryPage\"");
        string historyList = SliceBetween(xaml, "x:Name=\"HistoryListView\"", "x:Name=\"RulesPage\"");

        Assert.IsGreaterThanOrEqualTo(
            2,
            CountOccurrences(xaml, "<Expander"),
            "Processes and history should use expandable compact rows.");
        StringAssert.Contains(processList, "Source=\"{x:Bind Icon, Mode=OneWay}\"");
        StringAssert.Contains(processList, "x:Name=\"ProcessArchitectureBadge\"");
        StringAssert.Contains(processList, "x:Name=\"ProcessSummaryLine\"");
        StringAssert.Contains(processList, "x:Name=\"CopyProcessIdButton\"");
        StringAssert.Contains(processList, "Click=\"CopyValueButton_Click\"");
        StringAssert.Contains(processList, "Tag=\"{x:Bind ProcessId, Mode=OneWay}\"");
        StringAssert.Contains(processList, "x:Name=\"CopyProcessIdText\"");
        StringAssert.Contains(processList, "x:Name=\"CopyProcessIdGlyph\"");

        StringAssert.Contains(historyList, "Source=\"{x:Bind Icon, Mode=OneWay}\"");
        StringAssert.Contains(historyList, "x:Name=\"HistoryArchitectureBadge\"");
        StringAssert.Contains(historyList, "x:Name=\"HistorySummaryLine\"");
        StringAssert.Contains(historyList, "x:Name=\"CopyHistoryProcessIdButton\"");
        StringAssert.Contains(historyList, "Click=\"CopyValueButton_Click\"");
        StringAssert.Contains(historyList, "Tag=\"{x:Bind LastProcessId, Mode=OneWay}\"");
        StringAssert.Contains(historyList, "x:Name=\"CopyHistoryProcessIdText\"");
        StringAssert.Contains(historyList, "x:Name=\"CopyHistoryProcessIdGlyph\"");
    }

    [TestMethod]
    public void ProcessesAndHistoryUseFluentListPresentation()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        string processList = SliceBetween(xaml, "x:Name=\"ProcessListView\"", "x:Name=\"HistoryPage\"");
        string historyList = SliceBetween(xaml, "x:Name=\"HistoryListView\"", "x:Name=\"RulesPage\"");

        StringAssert.Contains(xaml, "x:Name=\"ProcessHeader\"");
        StringAssert.Contains(xaml, "x:Name=\"HistoryHeader\"");
        StringAssert.Contains(xaml, "x:Name=\"ProcessStatusTextBlock\"");
        StringAssert.Contains(xaml, "x:Name=\"HistoryStatusTextBlock\"");
        StringAssert.Contains(xaml, "x:Name=\"HistoryToolbar\"");
        StringAssert.Contains(xaml, "x:Key=\"RowCardStyle\"");
        StringAssert.Contains(xaml, "x:Key=\"ArchitectureBadgeStyle\"");
        StringAssert.Contains(xaml, "x:Key=\"DetailValueButtonStyle\"");
        StringAssert.Contains(xaml, "x:Key=\"IconCommandButtonStyle\"");
        StringAssert.Contains(xaml, "CardBackgroundFillColorDefaultBrush");
        StringAssert.Contains(xaml, "CardStrokeColorDefaultBrush");
        StringAssert.Contains(xaml, "<Setter Property=\"CornerRadius\" Value=\"8\" />");
        StringAssert.Contains(processList, "x:Name=\"ProcessDetailsPanel\"");
        StringAssert.Contains(historyList, "x:Name=\"HistoryDetailsPanel\"");
        Assert.IsGreaterThanOrEqualTo(
            6,
            CountOccurrences(xaml, "ToolTipService.ToolTip="),
            "Icon refresh, copy, and actions commands should expose tooltips.");
    }

    [TestMethod]
    public void ExpandedDetailsUseInlineCopyValuesAndDirectProcessActions()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        string processList = SliceBetween(xaml, "x:Name=\"ProcessListView\"", "x:Name=\"HistoryPage\"");
        string historyList = SliceBetween(xaml, "x:Name=\"HistoryListView\"", "x:Name=\"RulesPage\"");

        Assert.IsFalse(
            xaml.Contains("x:Key=\"DetailsPanelStyle\"", StringComparison.Ordinal),
            "Expanded details should be inline content, not a nested card style.");
        Assert.IsFalse(
            xaml.Contains("CopyProcessPathButton", StringComparison.Ordinal)
                || xaml.Contains("CopyHistoryPathButton", StringComparison.Ordinal),
            "Path copy should not be a repeated standalone copy button.");
        Assert.IsFalse(
            processList.Contains("Process actions", StringComparison.Ordinal)
                || processList.Contains("<Button.Flyout>", StringComparison.Ordinal),
            "Process details should expose direct actions instead of a more menu.");

        StringAssert.Contains(processList, "x:Name=\"CopyProcessIdButton\"");
        StringAssert.Contains(processList, "Text=\"{x:Bind ProcessId, Mode=OneWay}\"");
        StringAssert.Contains(processList, "x:Name=\"CopyProcessIdGlyph\"");
        StringAssert.Contains(historyList, "x:Name=\"CopyHistoryProcessIdButton\"");
        StringAssert.Contains(historyList, "Text=\"{x:Bind LastProcessId, Mode=OneWay}\"");
        StringAssert.Contains(historyList, "x:Name=\"CopyHistoryProcessIdGlyph\"");
        StringAssert.Contains(processList, "x:Name=\"EndProcessButton\"");
        StringAssert.Contains(processList, "x:Name=\"IgnoreProcessButton\"");
        StringAssert.Contains(processList, "Content=\"Ignore app\"");
        StringAssert.Contains(historyList, "x:Name=\"IgnoreHistoryAppButton\"");
        StringAssert.Contains(historyList, "Content=\"Ignore app\"");
        StringAssert.Contains(historyList, "Click=\"IgnoreHistoryAppButton_Click\"");
    }

    [TestMethod]
    public void MainWindowListRowsDoNotUseDefaultItemPadding()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        Assert.IsGreaterThanOrEqualTo(
            3,
            CountOccurrences(xaml, "<Setter Property=\"Padding\" Value=\"0\" />"),
            "Process, history, and rule list rows should remove ListViewItem default padding so headers and content share the same column origin.");
    }

    [TestMethod]
    public void HistoryFilterComboBoxItemsExposeStableTags()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));
        string code = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml.cs")));
        string historyPage = SliceBetween(xaml, "x:Name=\"HistoryPage\"", "x:Name=\"RulesPage\"");

        StringAssert.Contains(historyPage, "Tag=\"All\"");
        StringAssert.Contains(historyPage, "Tag=\"x86\"");
        StringAssert.Contains(historyPage, "Tag=\"x64\"");
        StringAssert.Contains(historyPage, "Tag=\"ARM64EC\"");
        StringAssert.Contains(historyPage, "Tag=\"Ignored\"");
        StringAssert.Contains(historyPage, "Tag=\"Not ignored\"");
        Assert.IsGreaterThanOrEqualTo(
            2,
            CountOccurrences(historyPage, "SelectedValuePath=\"Tag\""),
            "History filters should bind selected values to stable tags instead of relying on WinUI item container text.");
        StringAssert.Contains(code, "comboBox.SelectedValue is string selectedValue");
        StringAssert.Contains(code, "item.Tag is string tag");
    }

    private static string SliceBetween(string value, string startMarker, string endMarker)
    {
        int start = value.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"Could not find {startMarker}.");

        int end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, end, $"Could not find {endMarker}.");

        return value[start..end];
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;

        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {AppContext.BaseDirectory}.");
    }
}
