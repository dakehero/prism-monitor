using PrismMonitor.Core.Settings;

namespace PrismMonitor.App;

public sealed class NotificationLevelOption(NotificationLevel level, string displayName)
{
    public NotificationLevel Level { get; set; } = level;

    public string DisplayName { get; set; } = displayName;
}
