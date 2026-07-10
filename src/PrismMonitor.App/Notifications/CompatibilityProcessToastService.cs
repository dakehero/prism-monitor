using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PrismMonitor.App.Diagnostics;
using PrismMonitor.Core.Notifications;
using PrismMonitor.Core.Processes;
using PrismMonitor.App.Processes;

namespace PrismMonitor.App.Notifications;

internal sealed class CompatibilityProcessToastService : IDisposable
{
    private readonly IgnoredProcessStore _ignoredProcessStore;
    private readonly ProcessTerminator _processTerminator = new();
    private readonly AppNotificationManager _manager = AppNotificationManager.Default;
    private bool _registered;

    public CompatibilityProcessToastService(IgnoredProcessStore ignoredProcessStore)
    {
        _ignoredProcessStore = ignoredProcessStore;
    }

    public event EventHandler<NotificationProcessOpenRequestedEventArgs>? ProcessOpenRequested;

    public event EventHandler? RulesChanged;

    public void Register()
    {
        if (!AppNotificationManager.IsSupported() || _registered)
        {
            return;
        }

        try
        {
            _manager.NotificationInvoked += NotificationInvoked;
            _manager.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            _manager.NotificationInvoked -= NotificationInvoked;
            StartupDiagnostics.Write("CompatibilityProcessToastService.Register", ex);
        }
    }

    public void ShowNewProcess(CompatibilityProcessInfo process)
    {
        if (!_registered)
        {
            return;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddArgument(NotificationActivationParser.ActionKey, NotificationActivationParser.OpenAction)
            .AddArgument(NotificationActivationParser.ProcessIdKey, process.ProcessId.ToString())
            .AddArgument(NotificationActivationParser.ProcessNameKey, process.Name)
            .AddText("Compatibility-mode process detected")
            .AddText($"{process.Name} ({process.Architecture})")
            .AddButton(new AppNotificationButton("End now")
                .AddArgument(NotificationActivationParser.ActionKey, NotificationActivationParser.TerminateAction)
                .AddArgument(NotificationActivationParser.ProcessIdKey, process.ProcessId.ToString())
                .AddArgument(NotificationActivationParser.ProcessNameKey, process.Name))
            .AddButton(new AppNotificationButton("Always ignore")
                .AddArgument(NotificationActivationParser.ActionKey, NotificationActivationParser.IgnoreAction)
                .AddArgument(NotificationActivationParser.ProcessNameKey, process.Name))
            .SetTag(process.ProcessId.ToString())
            .BuildNotification();

        TryShow(notification);
    }

    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            _manager.NotificationInvoked -= NotificationInvoked;
            _manager.Unregister();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("CompatibilityProcessToastService.Dispose", ex);
        }

        _registered = false;
    }

    private async void NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        NotificationActivation activation = NotificationActivationParser.Parse(
            new Dictionary<string, string>(args.Arguments));

        await HandleActivationAsync(activation);
    }

    internal async Task HandleActivationAsync(NotificationActivation activation)
    {
        if (activation.Kind == NotificationActivationKind.OpenProcess
            && activation.ProcessId is int openProcessId
            && activation.ProcessName is string openProcessName)
        {
            ProcessOpenRequested?.Invoke(
                this,
                new NotificationProcessOpenRequestedEventArgs(openProcessId, openProcessName));
            return;
        }

        if (activation.Kind == NotificationActivationKind.TerminateProcess
            && activation.ProcessId is int terminateProcessId)
        {
            ProcessTerminationResult result = _processTerminator.Terminate(terminateProcessId);
            ShowStatus("End process", result.Message);
            return;
        }

        if (activation.Kind == NotificationActivationKind.IgnoreProcess
            && activation.ProcessName is string processName)
        {
            await _ignoredProcessStore.AddAsync(processName);
            RulesChanged?.Invoke(this, EventArgs.Empty);
            ShowStatus("Added to ignore list", processName);
        }
    }

    private void ShowStatus(string title, string message)
    {
        if (!_registered)
        {
            return;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification();

        TryShow(notification);
    }

    private void TryShow(AppNotification notification)
    {
        try
        {
            _manager.Show(notification);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("CompatibilityProcessToastService.Show", ex);
        }
    }
}

internal sealed class NotificationProcessOpenRequestedEventArgs(int processId, string processName) : EventArgs
{
    public int ProcessId { get; } = processId;

    public string ProcessName { get; } = processName;
}
