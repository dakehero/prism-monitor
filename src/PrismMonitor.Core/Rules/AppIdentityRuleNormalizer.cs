namespace PrismMonitor.Core.Rules;

public static class AppIdentityRuleNormalizer
{
    public static string NormalizeProcessName(string? processName)
    {
        string normalized = NormalizeValue(processName);
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized;
    }

    public static string NormalizeValue(string? value)
    {
        return (value ?? string.Empty).Trim();
    }
}
