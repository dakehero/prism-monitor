namespace PrismMonitor.Core.Processes;

public sealed record ProcessEnrichmentInfo(
    ProcessCompatibilityState Compatibility,
    string? Architecture = null,
    string? ExecutablePath = null,
    string? PackageIdentity = null,
    string? PublisherIdentity = null,
    string? IconCacheKey = null,
    ProcessEnrichmentLevel Level = ProcessEnrichmentLevel.Classification,
    ProcessIdentityFields AttemptedFields = ProcessIdentityFields.None,
    bool HasLimitedDetails = false,
    string? LastError = null)
{
    public static ProcessEnrichmentInfo Native { get; } = new(ProcessCompatibilityState.Native);

    public static ProcessEnrichmentInfo NotCompatibility => Native;

    public static ProcessEnrichmentInfo UnknownLimited(string? error)
    {
        return new ProcessEnrichmentInfo(
            ProcessCompatibilityState.Unknown,
            HasLimitedDetails: true,
            LastError: error);
    }
}
