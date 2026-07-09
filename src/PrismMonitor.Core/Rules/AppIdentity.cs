namespace PrismMonitor.Core.Rules;

public sealed record AppIdentity(
    string ProcessName,
    string? ExecutablePath = null,
    string? PackageIdentity = null,
    string? PublisherIdentity = null,
    string? Architecture = null);
