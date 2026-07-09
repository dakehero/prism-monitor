namespace PrismMonitor.Core.Processes;

public sealed record ProcessEnrichmentInfo(
    string? Architecture,
    string? ExecutablePath = null,
    string? PackageIdentity = null,
    string? PublisherIdentity = null)
{
    public static ProcessEnrichmentInfo NotCompatibility { get; } = new ProcessEnrichmentInfo(Architecture: null);
}
