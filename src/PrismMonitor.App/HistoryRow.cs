using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace PrismMonitor.App;

public sealed class HistoryRow : INotifyPropertyChanged
{
    private string _processName;
    private string _architecture;
    private int _launchCount;
    private string _firstSeen;
    private string _lastSeen;
    private int _lastProcessId;
    private string _launchCountText;
    private string _summaryLine;
    private string _timeRangeLine;
    private string _executablePath;
    private string _ignoredText;
    private ImageSource _icon;

    public HistoryRow(
        string processName,
        string architecture,
        int launchCount,
        string firstSeen,
        string lastSeen,
        int lastProcessId,
        string executablePath,
        string ignoredText,
        ImageSource icon)
    {
        _processName = processName;
        _architecture = architecture;
        _launchCount = launchCount;
        _firstSeen = firstSeen;
        _lastSeen = lastSeen;
        _lastProcessId = lastProcessId;
        _launchCountText = CreateLaunchCountText(launchCount);
        _summaryLine = CreateSummaryLine(launchCount, lastSeen);
        _timeRangeLine = CreateTimeRangeLine(firstSeen, lastSeen);
        _executablePath = executablePath;
        _ignoredText = ignoredText;
        _icon = icon;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProcessName
    {
        get => _processName;
        private set => SetProperty(ref _processName, value);
    }

    public string Architecture
    {
        get => _architecture;
        private set => SetProperty(ref _architecture, value);
    }

    public int LaunchCount
    {
        get => _launchCount;
        private set
        {
            if (SetProperty(ref _launchCount, value))
            {
                LaunchCountText = CreateLaunchCountText(value);
                SummaryLine = CreateSummaryLine(value, LastSeen);
            }
        }
    }

    public string LaunchCountText
    {
        get => _launchCountText;
        private set => SetProperty(ref _launchCountText, value);
    }

    public string FirstSeen
    {
        get => _firstSeen;
        private set
        {
            if (SetProperty(ref _firstSeen, value))
            {
                TimeRangeLine = CreateTimeRangeLine(value, LastSeen);
            }
        }
    }

    public string LastSeen
    {
        get => _lastSeen;
        private set
        {
            if (SetProperty(ref _lastSeen, value))
            {
                SummaryLine = CreateSummaryLine(LaunchCount, value);
                TimeRangeLine = CreateTimeRangeLine(FirstSeen, value);
            }
        }
    }

    public int LastProcessId
    {
        get => _lastProcessId;
        private set => SetProperty(ref _lastProcessId, value);
    }

    public string SummaryLine
    {
        get => _summaryLine;
        private set => SetProperty(ref _summaryLine, value);
    }

    public string TimeRangeLine
    {
        get => _timeRangeLine;
        private set => SetProperty(ref _timeRangeLine, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        private set => SetProperty(ref _executablePath, value);
    }

    public string IgnoredText
    {
        get => _ignoredText;
        private set => SetProperty(ref _ignoredText, value);
    }

    public ImageSource Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public void Update(
        string processName,
        string architecture,
        int launchCount,
        string firstSeen,
        string lastSeen,
        int lastProcessId,
        string executablePath,
        string ignoredText,
        ImageSource icon)
    {
        ProcessName = processName;
        Architecture = architecture;
        LaunchCount = launchCount;
        FirstSeen = firstSeen;
        LastSeen = lastSeen;
        LastProcessId = lastProcessId;
        ExecutablePath = executablePath;
        IgnoredText = ignoredText;
        Icon = icon;
    }

    private static string CreateLaunchCountText(int launchCount)
    {
        return launchCount == 1 ? "1 launch" : string.Concat(launchCount.ToString(), " launches");
    }

    private static string CreateTimeRangeLine(string firstSeen, string lastSeen)
    {
        return string.Concat("Last seen ", lastSeen, " · First seen ", firstSeen);
    }

    private static string CreateSummaryLine(int launchCount, string lastSeen)
    {
        return string.Concat(CreateLaunchCountText(launchCount), " · Last seen ", lastSeen);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
