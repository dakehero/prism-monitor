using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PrismMonitor.Core.Processes;
using PrismMonitor.App.Processes;

namespace PrismMonitor.App.Notifications;

internal sealed class CompatibilityProcessToastService : IDisposable
{
    private const string ActionKey = "action";
    private const string ProcessIdKey = "pid";
    private const string ProcessNameKey = "name";
    private const string TerminateAction = "terminate";
    private const string IgnoreAction = "ignore";
    private readonly IgnoredProcessStore _ignoredProcessStore;
    private readonly ProcessTerminator _processTerminator = new();
    private readonly AppNotificationManager _manager = AppNotificationManager.Default;
    private bool _registered;

    public CompatibilityProcessToastService(IgnoredProcessStore ignoredProcessStore)
    {
        _ignoredProcessStore = ignoredProcessStore;
    }

    public void Register()
    {
        if (!AppNotificationManager.IsSupported() || _registered)
        {
            return;
        }

        _manager.NotificationInvoked += NotificationInvoked;
        _manager.Register();
        _registered = true;
    }

    public void ShowNewProcess(CompatibilityProcessInfo process)
    {
        if (!_registered)
        {
            return;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddText("Compatibility-mode process detected")
            .AddText($"{process.Name} ({process.Architecture})")
            .AddButton(new AppNotificationButton("End now")
                .AddArgument(ActionKey, TerminateAction)
                .AddArgument(ProcessIdKey, process.ProcessId.ToString())
                .AddArgument(ProcessNameKey, process.Name))
            .AddButton(new AppNotificationButton("Always ignore")
                .AddArgument(ActionKey, IgnoreAction)
                .AddArgument(ProcessNameKey, process.Name))
            .SetTag(process.ProcessId.ToString())
            .BuildNotification();

        _manager.Show(notification);
    }

    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        _manager.NotificationInvoked -= NotificationInvoked;
        _manager.Unregister();
        _registered = false;
    }

    private async void NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (!args.Arguments.TryGetValue(ActionKey, out string? action))
        {
            return;
        }

        if (action == TerminateAction
            && args.Arguments.TryGetValue(ProcessIdKey, out string? processIdValue)
            && int.TryParse(processIdValue, out int processId))
        {
            ProcessTerminationResult result = _processTerminator.Terminate(processId);
            ShowStatus("End process", result.Message);
            return;
        }

        if (action == IgnoreAction
            && args.Arguments.TryGetValue(ProcessNameKey, out string? processName))
        {
            await _ignoredProcessStore.AddAsync(processName);
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

        _manager.Show(notification);
    }
}
