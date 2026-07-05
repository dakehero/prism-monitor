namespace PrismMonitor.Core.Processes;

public static class PeImageArchitectureDetector
{
    public static bool HasArm64XMetadata(IEnumerable<string> sectionNames)
    {
        return sectionNames.Any(sectionName =>
            string.Equals(sectionName, ".a64xrm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sectionName, ".hexpthk", StringComparison.OrdinalIgnoreCase));
    }
}
