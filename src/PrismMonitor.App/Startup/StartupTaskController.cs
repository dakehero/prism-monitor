using Windows.ApplicationModel;

namespace PrismMonitor.App.Startup;

internal sealed record StartupTaskStatus(bool IsEnabled, bool CanChange, string Description);

internal sealed class StartupTaskController
{
    private const string TaskId = "PrismMonitorStartup";

    public async Task<StartupTaskStatus> GetStatusAsync()
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            return CreateStatus(task.State);
        }
        catch (Exception)
        {
            return new StartupTaskStatus(
                IsEnabled: false,
                CanChange: false,
                Description: "Startup control is available in the installed app.");
        }
    }

    public async Task<StartupTaskStatus> SetEnabledAsync(bool enabled)
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(TaskId);
            StartupTaskState state;

            if (enabled)
            {
                state = await task.RequestEnableAsync();
            }
            else
            {
                task.Disable();
                state = task.State;
            }

            return CreateStatus(state);
        }
        catch (Exception)
        {
            StartupTaskStatus status = await GetStatusAsync();
            return status with { Description = "Could not update the Windows startup setting." };
        }
    }

    private static StartupTaskStatus CreateStatus(StartupTaskState state)
    {
        return state switch
        {
            StartupTaskState.Enabled => new(true, true, "Starts in the tray after you sign in."),
            StartupTaskState.EnabledByPolicy => new(true, false, "Enabled by your organization."),
            StartupTaskState.DisabledByUser => new(
                false,
                false,
                "Disabled in Windows Startup Apps. Re-enable it there."),
            StartupTaskState.DisabledByPolicy => new(false, false, "Disabled by your organization."),
            _ => new(false, true, "Does not start automatically after you sign in.")
        };
    }
}
