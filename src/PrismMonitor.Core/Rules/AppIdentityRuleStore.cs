using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrismMonitor.Core.Rules;

public sealed class AppIdentityRuleStore(string rulesFilePath, string? legacyIgnoredNamesFilePath = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<AppIdentityRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadRulesOrMigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddOrUpdateRuleAsync(AppIdentityRule rule, CancellationToken cancellationToken = default)
    {
        AppIdentityRule normalizedRule = NormalizeRule(rule);
        if (!HasAnyMatchField(normalizedRule) || normalizedRule.Targets == SuppressionTarget.None)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AppIdentityRule> rules = await ReadRulesOrMigrateAsync(cancellationToken).ConfigureAwait(false);
            string ruleKey = GetRuleKey(normalizedRule);
            rules.RemoveAll(existingRule => string.Equals(GetRuleKey(existingRule), ruleKey, StringComparison.OrdinalIgnoreCase));
            rules.Add(normalizedRule);

            await WriteRulesAsync(NormalizeAndSort(rules), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveRuleAsync(AppIdentityRule rule, CancellationToken cancellationToken = default)
    {
        AppIdentityRule normalizedRule = NormalizeRule(rule);
        if (!HasAnyMatchField(normalizedRule))
        {
            return;
        }

        string ruleKey = GetRuleKey(normalizedRule);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AppIdentityRule> rules = await ReadRulesOrMigrateAsync(cancellationToken).ConfigureAwait(false);
            rules.RemoveAll(existingRule => string.Equals(GetRuleKey(existingRule), ruleKey, StringComparison.OrdinalIgnoreCase));
            await WriteRulesAsync(NormalizeAndSort(rules), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddProcessNameRuleAsync(
        string processName,
        SuppressionTarget targets,
        CancellationToken cancellationToken = default)
    {
        AppIdentityRule rule = CreateProcessNameRule(processName, targets);
        if (rule.ProcessName is null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AppIdentityRule> rules = await ReadRulesOrMigrateAsync(cancellationToken).ConfigureAwait(false);
            if (!rules.Any(existingRule => IsSameProcessNameRule(existingRule, rule.ProcessName, targets)))
            {
                rules.Add(rule);
            }

            await WriteRulesAsync(NormalizeAndSort(rules), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveProcessNameRuleAsync(string processName, CancellationToken cancellationToken = default)
    {
        string normalizedProcessName = AppIdentityRuleNormalizer.NormalizeProcessName(processName);
        if (normalizedProcessName.Length == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<AppIdentityRule> rules = await ReadRulesOrMigrateAsync(cancellationToken).ConfigureAwait(false);
            rules.RemoveAll(rule => IsProcessNameOnlyRule(rule)
                && rule.Targets == SuppressionTarget.All
                && string.Equals(
                    AppIdentityRuleNormalizer.NormalizeProcessName(rule.ProcessName),
                    normalizedProcessName,
                    StringComparison.OrdinalIgnoreCase));

            await WriteRulesAsync(NormalizeAndSort(rules), cancellationToken).ConfigureAwait(false);
            await RemoveLegacyNameAsync(normalizedProcessName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static AppIdentityRule CreateRuleForIdentity(AppIdentity identity, SuppressionTarget targets)
    {
        string displayName = AppIdentityRuleNormalizer.NormalizeValue(identity.ProcessName);
        string? packageIdentity = ToNullable(AppIdentityRuleNormalizer.NormalizeValue(identity.PackageIdentity));
        string? executablePath = packageIdentity is null
            ? ToNullable(AppIdentityRuleNormalizer.NormalizeValue(identity.ExecutablePath))
            : null;
        string? publisherIdentity = packageIdentity is null && executablePath is null
            ? ToNullable(AppIdentityRuleNormalizer.NormalizeValue(identity.PublisherIdentity))
            : null;
        string? processName = packageIdentity is null && executablePath is null && publisherIdentity is null
            ? ToNullable(AppIdentityRuleNormalizer.NormalizeProcessName(identity.ProcessName))
            : null;
        string? architecture = packageIdentity is not null
            || executablePath is not null
            || publisherIdentity is not null
            || processName is not null
                ? ToNullable(AppIdentityRuleNormalizer.NormalizeValue(identity.Architecture))
                : null;

        return new AppIdentityRule(
            displayName.Length > 0 ? displayName : "App identity rule",
            ProcessName: processName,
            ExecutablePath: executablePath,
            PackageIdentity: packageIdentity,
            PublisherIdentity: publisherIdentity,
            Architecture: architecture,
            Targets: targets);
    }

    private async Task<List<AppIdentityRule>> ReadRulesOrMigrateAsync(CancellationToken cancellationToken)
    {
        RuleReadResult readResult = await ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        List<AppIdentityRule> rules = readResult.Rules;
        if (legacyIgnoredNamesFilePath is null || !File.Exists(legacyIgnoredNamesFilePath))
        {
            if (readResult.NeedsRewrite)
            {
                await WriteRulesAsync(rules, cancellationToken).ConfigureAwait(false);
            }

            return rules;
        }

        IReadOnlyList<string> legacyNames = await ReadLegacyNamesAsync(cancellationToken).ConfigureAwait(false);
        List<AppIdentityRule> mergedRules = NormalizeAndSort(rules.Concat(legacyNames.Select(CreateProcessNameRule))).ToList();
        if (readResult.NeedsRewrite || mergedRules.Count != rules.Count)
        {
            await WriteRulesAsync(mergedRules, cancellationToken).ConfigureAwait(false);
        }

        return mergedRules;
    }

    private async Task<RuleReadResult> ReadRulesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(rulesFilePath))
        {
            return new RuleReadResult([], NeedsRewrite: false);
        }

        try
        {
            string json = await File.ReadAllTextAsync(rulesFilePath, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            bool isBareArray = document.RootElement.ValueKind == JsonValueKind.Array;
            AppIdentityRule[]? rules = isBareArray
                ? JsonSerializer.Deserialize(json, AppIdentityRuleJsonContext.Default.AppIdentityRuleArray)
                : JsonSerializer.Deserialize(json, AppIdentityRuleJsonContext.Default.AppIdentityRuleDocument)?.Rules;

            return new RuleReadResult(NormalizeAndSort(rules ?? []).ToList(), NeedsRewrite: isBareArray);
        }
        catch (JsonException)
        {
            return new RuleReadResult([], NeedsRewrite: false);
        }
        catch (IOException)
        {
            return new RuleReadResult([], NeedsRewrite: false);
        }
        catch (UnauthorizedAccessException)
        {
            return new RuleReadResult([], NeedsRewrite: false);
        }
    }

    private async Task<IReadOnlyList<string>> ReadLegacyNamesAsync(CancellationToken cancellationToken)
    {
        if (legacyIgnoredNamesFilePath is null || !File.Exists(legacyIgnoredNamesFilePath))
        {
            return [];
        }

        try
        {
            await using FileStream stream = File.OpenRead(legacyIgnoredNamesFilePath);
            string[]? names = await JsonSerializer.DeserializeAsync(
                    stream,
                    AppIdentityRuleJsonContext.Default.StringArray,
                    cancellationToken)
                .ConfigureAwait(false);

            return names ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task RemoveLegacyNameAsync(string normalizedProcessName, CancellationToken cancellationToken)
    {
        if (legacyIgnoredNamesFilePath is null || !File.Exists(legacyIgnoredNamesFilePath))
        {
            return;
        }

        IReadOnlyList<string> legacyNames = await ReadLegacyNamesAsync(cancellationToken).ConfigureAwait(false);
        string[] retainedNames = legacyNames
            .Where(name => !string.Equals(
                AppIdentityRuleNormalizer.NormalizeProcessName(name),
                normalizedProcessName,
                StringComparison.OrdinalIgnoreCase))
            .Select(AppIdentityRuleNormalizer.NormalizeProcessName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using FileStream stream = File.Create(legacyIgnoredNamesFilePath);
        await JsonSerializer.SerializeAsync(
                stream,
                retainedNames,
                AppIdentityRuleJsonContext.Default.StringArray,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task WriteRulesAsync(IEnumerable<AppIdentityRule> rules, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(rulesFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        AppIdentityRuleDocument document = new(Version: 1, NormalizeAndSort(rules).ToArray());
        await using FileStream stream = File.Create(rulesFilePath);
        await JsonSerializer.SerializeAsync(
                stream,
                document,
                AppIdentityRuleJsonContext.Default.AppIdentityRuleDocument,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static AppIdentityRule CreateProcessNameRule(string processName)
    {
        return CreateProcessNameRule(processName, SuppressionTarget.All);
    }

    private static AppIdentityRule CreateProcessNameRule(string processName, SuppressionTarget targets)
    {
        string normalizedName = AppIdentityRuleNormalizer.NormalizeProcessName(processName);
        return normalizedName.Length == 0
            ? new AppIdentityRule(string.Empty, Targets: targets)
            : new AppIdentityRule(normalizedName, ProcessName: normalizedName, Targets: targets);
    }

    private static IEnumerable<AppIdentityRule> NormalizeAndSort(IEnumerable<AppIdentityRule> rules)
    {
        return rules
            .Select(NormalizeRule)
            .Where(rule => HasAnyMatchField(rule) && rule.Targets != SuppressionTarget.None)
            .DistinctBy(GetRuleKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(rule => rule.ProcessName ?? rule.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.PackageIdentity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.PublisherIdentity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Architecture, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Targets);
    }

    private static AppIdentityRule NormalizeRule(AppIdentityRule rule)
    {
        string? processName = ToNullable(AppIdentityRuleNormalizer.NormalizeProcessName(rule.ProcessName));
        string displayName = AppIdentityRuleNormalizer.NormalizeValue(rule.DisplayName);
        return rule with
        {
            DisplayName = displayName.Length > 0 ? displayName : processName ?? "App identity rule",
            ProcessName = processName,
            ExecutablePath = ToNullable(AppIdentityRuleNormalizer.NormalizeValue(rule.ExecutablePath)),
            PackageIdentity = ToNullable(AppIdentityRuleNormalizer.NormalizeValue(rule.PackageIdentity)),
            PublisherIdentity = ToNullable(AppIdentityRuleNormalizer.NormalizeValue(rule.PublisherIdentity)),
            Architecture = ToNullable(AppIdentityRuleNormalizer.NormalizeValue(rule.Architecture))
        };
    }

    private static bool HasAnyMatchField(AppIdentityRule rule)
    {
        return !string.IsNullOrWhiteSpace(rule.ProcessName)
            || !string.IsNullOrWhiteSpace(rule.ExecutablePath)
            || !string.IsNullOrWhiteSpace(rule.PackageIdentity)
            || !string.IsNullOrWhiteSpace(rule.PublisherIdentity);
    }

    private static bool IsSameProcessNameRule(AppIdentityRule rule, string processName, SuppressionTarget targets)
    {
        return IsProcessNameOnlyRule(rule)
            && rule.Targets == targets
            && string.Equals(
                AppIdentityRuleNormalizer.NormalizeProcessName(rule.ProcessName),
                AppIdentityRuleNormalizer.NormalizeProcessName(processName),
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProcessNameOnlyRule(AppIdentityRule rule)
    {
        return !string.IsNullOrWhiteSpace(rule.ProcessName)
            && string.IsNullOrWhiteSpace(rule.ExecutablePath)
            && string.IsNullOrWhiteSpace(rule.PackageIdentity)
            && string.IsNullOrWhiteSpace(rule.PublisherIdentity)
            && string.IsNullOrWhiteSpace(rule.Architecture);
    }

    private static string GetRuleKey(AppIdentityRule rule)
    {
        return string.Join(
            '\u001f',
            rule.ProcessName,
            rule.ExecutablePath,
            rule.PackageIdentity,
            rule.PublisherIdentity,
            rule.Architecture,
            ((int)rule.Targets).ToString());
    }

    private static string? ToNullable(string value)
    {
        return value.Length == 0 ? null : value;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppIdentityRuleDocument))]
[JsonSerializable(typeof(AppIdentityRule))]
[JsonSerializable(typeof(AppIdentityRule[]))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AppIdentityRuleJsonContext : JsonSerializerContext;

internal sealed record RuleReadResult(List<AppIdentityRule> Rules, bool NeedsRewrite);
