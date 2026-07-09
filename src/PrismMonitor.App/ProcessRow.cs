using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace PrismMonitor.App;

public sealed class ProcessRow : INotifyPropertyChanged
{
    private string _name;
    private string _architecture;
    private string _cpuTime;
    private string _summaryLine;
    private string _detailsLine;
    private string _executablePath;
    private string _packageIdentity;
    private string _publisherIdentity;
    private ImageSource _icon;

    public ProcessRow(
        string name,
        int processId,
        string architecture,
        string cpuTime,
        string executablePath,
        string packageIdentity,
        string publisherIdentity,
        ImageSource icon)
    {
        _name = name;
        ProcessId = processId;
        _architecture = architecture;
        _cpuTime = cpuTime;
        _summaryLine = CreateSummaryLine(cpuTime);
        _detailsLine = CreateDetailsLine(processId, cpuTime);
        _executablePath = executablePath;
        _packageIdentity = packageIdentity;
        _publisherIdentity = publisherIdentity;
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
        private set
        {
            if (SetProperty(ref _cpuTime, value))
            {
                SummaryLine = CreateSummaryLine(value);
                DetailsLine = CreateDetailsLine(ProcessId, value);
            }
        }
    }

    public string SummaryLine
    {
        get => _summaryLine;
        private set => SetProperty(ref _summaryLine, value);
    }

    public string DetailsLine
    {
        get => _detailsLine;
        private set => SetProperty(ref _detailsLine, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        private set => SetProperty(ref _executablePath, value);
    }

    public string PackageIdentity
    {
        get => _packageIdentity;
        private set => SetProperty(ref _packageIdentity, value);
    }

    public string PublisherIdentity
    {
        get => _publisherIdentity;
        private set => SetProperty(ref _publisherIdentity, value);
    }

    public ImageSource Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public void Update(
        string name,
        string architecture,
        string cpuTime,
        string executablePath,
        string packageIdentity,
        string publisherIdentity,
        ImageSource icon)
    {
        Name = name;
        Architecture = architecture;
        CpuTime = cpuTime;
        ExecutablePath = executablePath;
        PackageIdentity = packageIdentity;
        PublisherIdentity = publisherIdentity;
        Icon = icon;
    }

    private static string CreateDetailsLine(int processId, string cpuTime)
    {
        return string.Concat("PID ", processId.ToString(), " · CPU time ", cpuTime);
    }

    private static string CreateSummaryLine(string cpuTime)
    {
        return string.Concat("CPU time ", cpuTime);
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
