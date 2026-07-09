namespace PrismMonitor.Core.Rules;

public sealed record AppIdentityRule(
    string DisplayName,
    string? ProcessName = null,
    string? ExecutablePath = null,
    string? PackageIdentity = null,
    string? PublisherIdentity = null,
    string? Architecture = null,
    SuppressionTarget Targets = SuppressionTarget.All);
