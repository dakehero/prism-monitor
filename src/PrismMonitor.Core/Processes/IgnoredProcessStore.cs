using System.Text.Json;
using System.Text.Json.Serialization;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.Processes;

public sealed class IgnoredProcessStore(string filePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AppIdentityRuleStore _ruleStore = new(
        Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "app-rules.json"),
        filePath);

    public async Task<IReadOnlyList<string>> GetIgnoredNamesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AppIdentityRule> rules = await _ruleStore.GetRulesAsync(cancellationToken).ConfigureAwait(false);
        return NormalizeAndSort(rules
            .Where(rule => rule.Targets == SuppressionTarget.All
                && string.IsNullOrWhiteSpace(rule.ExecutablePath)
                && string.IsNullOrWhiteSpace(rule.PackageIdentity)
                && string.IsNullOrWhiteSpace(rule.PublisherIdentity)
                && string.IsNullOrWhiteSpace(rule.Architecture))
            .Select(rule => rule.ProcessName ?? string.Empty));
    }

    public Task<IReadOnlyList<AppIdentityRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        return _ruleStore.GetRulesAsync(cancellationToken);
    }

    public async Task AddRuleAsync(AppIdentityRule rule, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _ruleStore.AddOrUpdateRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> names = await GetIgnoredNamesAsync(cancellationToken).ConfigureAwait(false);
            await WriteNamesAsync(names, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveRuleAsync(AppIdentityRule rule, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsLegacyNameRule(rule))
            {
                await _ruleStore.RemoveProcessNameRuleAsync(rule.ProcessName!, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _ruleStore.RemoveRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<string> names = await GetIgnoredNamesAsync(cancellationToken).ConfigureAwait(false);
            await WriteNamesAsync(names, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(string processName, CancellationToken cancellationToken = default)
    {
        string normalizedName = IgnoredProcessFilter.NormalizeName(processName);
        if (normalizedName.Length == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _ruleStore.AddProcessNameRuleAsync(normalizedName, SuppressionTarget.All, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<string> names = await GetIgnoredNamesAsync(cancellationToken).ConfigureAwait(false);
            await WriteNamesAsync(names, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string processName, CancellationToken cancellationToken = default)
    {
        string normalizedName = IgnoredProcessFilter.NormalizeName(processName);
        if (normalizedName.Length == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _ruleStore.RemoveProcessNameRuleAsync(normalizedName, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> names = await GetIgnoredNamesAsync(cancellationToken).ConfigureAwait(false);
            await WriteNamesAsync(names, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string[] normalizedNames = NormalizeAndSort(names).ToArray();
        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(
                stream,
                normalizedNames,
                IgnoredProcessJsonContext.Default.StringArray,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<string> NormalizeAndSort(IEnumerable<string> names)
    {
        return names
            .Select(IgnoredProcessFilter.NormalizeName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLegacyNameRule(AppIdentityRule rule)
    {
        return rule.Targets == SuppressionTarget.All
            && !string.IsNullOrWhiteSpace(rule.ProcessName)
            && string.IsNullOrWhiteSpace(rule.ExecutablePath)
            && string.IsNullOrWhiteSpace(rule.PackageIdentity)
            && string.IsNullOrWhiteSpace(rule.PublisherIdentity)
            && string.IsNullOrWhiteSpace(rule.Architecture);
    }
}

[JsonSerializable(typeof(string[]))]
internal sealed partial class IgnoredProcessJsonContext : JsonSerializerContext;
