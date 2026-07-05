using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace PrismMonitor.App;

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
