namespace PrismMonitor.Core.Processes;

public readonly record struct ProcessEnrichmentRequest(
    ProcessEnrichmentLevel Level,
    ProcessIdentityFields IdentityFields)
{
    public static ProcessEnrichmentRequest Classification { get; } =
        new(ProcessEnrichmentLevel.Classification, ProcessIdentityFields.None);

    public static ProcessEnrichmentRequest Full { get; } =
        new(ProcessEnrichmentLevel.Full, ProcessIdentityFields.All);
}
