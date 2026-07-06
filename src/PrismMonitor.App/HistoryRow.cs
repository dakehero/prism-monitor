using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PrismMonitor.App;

public sealed class HistoryRow : INotifyPropertyChanged
{
    private string _processName;
    private string _architecture;
    private int _launchCount;
    private string _firstSeen;
    private string _lastSeen;
    private string _executablePath;
    private string _ignoredText;

    public HistoryRow(
        string processName,
        string architecture,
        int launchCount,
        string firstSeen,
        string lastSeen,
        string executablePath,
        string ignoredText)
    {
        _processName = processName;
        _architecture = architecture;
        _launchCount = launchCount;
        _firstSeen = firstSeen;
        _lastSeen = lastSeen;
        _executablePath = executablePath;
        _ignoredText = ignoredText;
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
        private set => SetProperty(ref _launchCount, value);
    }

    public string FirstSeen
    {
        get => _firstSeen;
        private set => SetProperty(ref _firstSeen, value);
    }

    public string LastSeen
    {
        get => _lastSeen;
        private set => SetProperty(ref _lastSeen, value);
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

    public void Update(
        string processName,
        string architecture,
        int launchCount,
        string firstSeen,
        string lastSeen,
        string executablePath,
        string ignoredText)
    {
        ProcessName = processName;
        Architecture = architecture;
        LaunchCount = launchCount;
        FirstSeen = firstSeen;
        LastSeen = lastSeen;
        ExecutablePath = executablePath;
        IgnoredText = ignoredText;
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
