namespace PrismMonitor.Core.Rules;

public static class AppIdentityRuleEvaluator
{
    public static bool IsSuppressed(
        AppIdentity identity,
        IEnumerable<AppIdentityRule> rules,
        SuppressionTarget target)
    {
        return rules.Any(rule => MatchesTarget(rule, target) && MatchesIdentity(identity, rule));
    }

    internal static bool MatchesIdentity(AppIdentity identity, AppIdentityRule rule)
    {
        bool hasMatchField = false;
        bool hasPrimaryMatchField = false;

        if (!MatchesOptionalProcessName(rule.ProcessName, identity.ProcessName, ref hasPrimaryMatchField))
        {
            return false;
        }

        if (!MatchesOptionalString(rule.ExecutablePath, identity.ExecutablePath, ref hasPrimaryMatchField))
        {
            return false;
        }

        if (!MatchesOptionalString(rule.PackageIdentity, identity.PackageIdentity, ref hasPrimaryMatchField))
        {
            return false;
        }

        if (!MatchesOptionalString(rule.PublisherIdentity, identity.PublisherIdentity, ref hasPrimaryMatchField))
        {
            return false;
        }

        if (!MatchesOptionalString(rule.Architecture, identity.Architecture, ref hasMatchField))
        {
            return false;
        }

        return hasPrimaryMatchField;
    }

    private static bool MatchesTarget(AppIdentityRule rule, SuppressionTarget target)
    {
        return target != SuppressionTarget.None && (rule.Targets & target) == target;
    }

    private static bool MatchesOptionalProcessName(
        string? expected,
        string actual,
        ref bool hasMatchField)
    {
        string normalizedExpected = AppIdentityRuleNormalizer.NormalizeProcessName(expected);
        if (normalizedExpected.Length == 0)
        {
            return true;
        }

        hasMatchField = true;
        return string.Equals(
            normalizedExpected,
            AppIdentityRuleNormalizer.NormalizeProcessName(actual),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesOptionalString(string? expected, string? actual, ref bool hasMatchField)
    {
        string normalizedExpected = AppIdentityRuleNormalizer.NormalizeValue(expected);
        if (normalizedExpected.Length == 0)
        {
            return true;
        }

        hasMatchField = true;
        return string.Equals(
            normalizedExpected,
            AppIdentityRuleNormalizer.NormalizeValue(actual),
            StringComparison.OrdinalIgnoreCase);
    }
}
