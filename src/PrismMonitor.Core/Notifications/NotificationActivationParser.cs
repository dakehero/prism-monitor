namespace PrismMonitor.Core.Notifications;

public static class NotificationActivationParser
{
    public const string ActionKey = "action";
    public const string ProcessIdKey = "pid";
    public const string ProcessNameKey = "name";
    public const string OpenAction = "open";
    public const string TerminateAction = "terminate";
    public const string IgnoreAction = "ignore";

    public static NotificationActivation Parse(IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue(ActionKey, out string? action))
        {
            return NotificationActivation.None;
        }

        return action switch
        {
            OpenAction => ParseOpen(arguments),
            TerminateAction => ParseTerminate(arguments),
            IgnoreAction => ParseIgnore(arguments),
            _ => NotificationActivation.None
        };
    }

    private static NotificationActivation ParseOpen(IReadOnlyDictionary<string, string> arguments)
    {
        return TryGetProcessId(arguments, out int processId)
            && TryGetProcessName(arguments, out string? processName)
                ? new NotificationActivation(NotificationActivationKind.OpenProcess, processId, processName)
                : NotificationActivation.None;
    }

    private static NotificationActivation ParseTerminate(IReadOnlyDictionary<string, string> arguments)
    {
        return TryGetProcessId(arguments, out int processId)
            ? new NotificationActivation(NotificationActivationKind.TerminateProcess, processId)
            : NotificationActivation.None;
    }

    private static NotificationActivation ParseIgnore(IReadOnlyDictionary<string, string> arguments)
    {
        return TryGetProcessName(arguments, out string? processName)
            ? new NotificationActivation(NotificationActivationKind.IgnoreProcess, ProcessName: processName)
            : NotificationActivation.None;
    }

    private static bool TryGetProcessId(IReadOnlyDictionary<string, string> arguments, out int processId)
    {
        if (arguments.TryGetValue(ProcessIdKey, out string? processIdValue)
            && int.TryParse(processIdValue, out processId))
        {
            return true;
        }

        processId = 0;
        return false;
    }

    private static bool TryGetProcessName(IReadOnlyDictionary<string, string> arguments, out string? processName)
    {
        if (arguments.TryGetValue(ProcessNameKey, out processName)
            && !string.IsNullOrWhiteSpace(processName))
        {
            return true;
        }

        processName = null;
        return false;
    }
}
