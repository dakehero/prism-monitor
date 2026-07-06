namespace PrismMonitor.Core.History;

public sealed record LaunchHistoryQuery(
    string? Text = null,
    string? Architecture = null,
    LaunchHistoryIgnoredState IgnoredState = LaunchHistoryIgnoredState.All);

public enum LaunchHistoryIgnoredState
{
    All,
    IgnoredOnly,
    NotIgnoredOnly
}
