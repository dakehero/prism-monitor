namespace PrismMonitor.Core.Processes;

[Flags]
public enum ProcessIdentityFields
{
    None = 0,
    ExecutablePath = 1,
    PackageIdentity = 2,
    PublisherIdentity = 4,
    All = ExecutablePath | PackageIdentity | PublisherIdentity
}
