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
        StringAssert.Contains(xaml, "x:Name=\"FilterListView\"");
        Assert.IsGreaterThanOrEqualTo(
            CountOccurrences(xaml, "ScrollViewer.VerticalScrollBarVisibility=\"Auto\""),
            3,
            "Process, history, and filter lists should explicitly expose vertical scrollbars.");
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
    public void MainWindowTablesUseLeftAlignedText()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        string processList = SliceBetween(xaml, "x:Name=\"ProcessListView\"", "x:Name=\"HistoryPage\"");
        string historyList = SliceBetween(xaml, "x:Name=\"HistoryListView\"", "x:Name=\"FiltersPage\"");
        string filterList = SliceBetween(xaml, "x:Name=\"FilterListView\"", "x:Name=\"SettingsPage\"");

        Assert.IsGreaterThanOrEqualTo(
            9,
            CountOccurrences(processList, "TextAlignment=\"Left\""),
            "Process list headers and row text should explicitly align text to the left.");
        Assert.IsGreaterThanOrEqualTo(
            14,
            CountOccurrences(historyList, "TextAlignment=\"Left\""),
            "History list headers and row text should explicitly align text to the left.");
        Assert.IsGreaterThanOrEqualTo(
            2,
            CountOccurrences(filterList, "TextAlignment=\"Left\""),
            "Filter list headers and row text should explicitly align text to the left.");
        Assert.IsGreaterThanOrEqualTo(
            3,
            CountOccurrences(xaml, "<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\" />"),
            "Process, history, and filter list rows should stretch so left-aligned text starts at the column edge.");
    }

    [TestMethod]
    public void MainWindowListRowsDoNotUseDefaultItemPadding()
    {
        string xaml = File.ReadAllText(FindRepoFile(Path.Combine("src", "PrismMonitor.App", "MainWindow.xaml")));

        Assert.IsGreaterThanOrEqualTo(
            3,
            CountOccurrences(xaml, "<Setter Property=\"Padding\" Value=\"0\" />"),
            "Process, history, and filter list rows should remove ListViewItem default padding so headers and content share the same column origin.");
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
