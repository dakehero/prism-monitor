using System.Text.Json;
using System.Text.Json.Serialization;
using PrismMonitor.Core.Processes;
using PrismMonitor.Core.Rules;

namespace PrismMonitor.Core.History;

public sealed class LaunchHistoryStore(string eventsFilePath, string summaryFilePath)
{
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AppendAsync(LaunchHistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(eventsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(
                historyEvent,
                LaunchHistoryJsonContext.Default.LaunchHistoryEvent);

            await File.AppendAllTextAsync(eventsFilePath, json + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);

            await RebuildSummaryAsync(historyEvent.DetectedAt, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LaunchHistorySummary>> GetSummaryAsync(
        DateTimeOffset now,
        IReadOnlySet<string> ignoredNames,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<LaunchHistorySummary> summaries = File.Exists(summaryFilePath)
                ? await ReadSummaryAsync(now, cancellationToken).ConfigureAwait(false)
                : await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);

            return ApplyIgnoredNames(summaries, ignoredNames);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<LaunchHistorySummary>> GetSummaryAsync(
        DateTimeOffset now,
        string[] ignoredNames,
        CancellationToken cancellationToken = default)
    {
        return GetSummaryAsync(
            now,
            ignoredNames.ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    public async Task<IReadOnlyList<LaunchHistorySummary>> GetSummaryWithRulesAsync(
        DateTimeOffset now,
        IReadOnlyList<AppIdentityRule> rules,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<LaunchHistorySummary> summaries = File.Exists(summaryFilePath)
                ? await ReadSummaryAsync(now, cancellationToken).ConfigureAwait(false)
                : await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);

            return AppIdentityRuleFilter.ApplyHistoryState(summaries, rules);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DeleteIfExists(eventsFilePath);
            DeleteIfExists(summaryFilePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<LaunchHistorySummary>> RebuildSummaryAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<LaunchHistoryEvent> events = await ReadRetainedEventsAsync(now, cancellationToken)
            .ConfigureAwait(false);

        List<LaunchHistorySummary> summaries = BuildSummaries(events);
        await WriteEventsAsync(events, cancellationToken).ConfigureAwait(false);
        await WriteSummaryAsync(summaries, cancellationToken).ConfigureAwait(false);

        return summaries;
    }

    private async Task<List<LaunchHistorySummary>> ReadSummaryAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        DateTimeOffset cutoff = now - RetentionWindow;

        try
        {
            await using FileStream stream = File.OpenRead(summaryFilePath);
            LaunchHistorySummary[]? summaries = await JsonSerializer.DeserializeAsync(
                    stream,
                    LaunchHistoryJsonContext.Default.LaunchHistorySummaryArray,
                    cancellationToken)
                .ConfigureAwait(false);

            LaunchHistorySummary[] loadedSummaries = summaries ?? [];
            if (loadedSummaries.Any(summary => summary is not null && summary.FirstSeenAt < cutoff))
            {
                return await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(eventsFilePath)
                && loadedSummaries.Any(summary => IsValidSummary(summary, cutoff, now) && summary.LastProcessId <= 0))
            {
                return await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(eventsFilePath)
                && await NeedsIdentitySummaryRebuildAsync(loadedSummaries, now, cancellationToken).ConfigureAwait(false))
            {
                return await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);
            }

            return loadedSummaries
                .Where(summary => IsValidSummary(summary, cutoff, now))
                .OrderByDescending(summary => summary.LastSeenAt)
                .ThenBy(summary => summary.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return await RebuildSummaryAsync(now, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task<List<LaunchHistoryEvent>> ReadRetainedEventsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(eventsFilePath))
        {
            return [];
        }

        DateTimeOffset cutoff = now - RetentionWindow;
        List<LaunchHistoryEvent> events = [];

        try
        {
            foreach (string line in await File.ReadAllLinesAsync(eventsFilePath, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    LaunchHistoryEvent? historyEvent = JsonSerializer.Deserialize(
                        line,
                        LaunchHistoryJsonContext.Default.LaunchHistoryEvent);

                    if (historyEvent is not null && IsValidEvent(historyEvent, cutoff, now))
                    {
                        events.Add(historyEvent);
                    }
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return events;
    }

    private async Task<bool> NeedsIdentitySummaryRebuildAsync(
        IReadOnlyList<LaunchHistorySummary> loadedSummaries,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<LaunchHistoryEvent> events = await ReadRetainedEventsAsync(now, cancellationToken).ConfigureAwait(false);
        if (events.Count == 0)
        {
            return false;
        }

        HashSet<string> eventKeys = events
            .Where(historyEvent => IsValidEvent(historyEvent, DateTimeOffset.MinValue, now))
            .Select(GetHistoryEventIdentityKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> summaryKeys = loadedSummaries
            .Where(summary => IsValidSummary(summary, DateTimeOffset.MinValue, now))
            .Select(GetSummaryIdentityKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return eventKeys.Count != summaryKeys.Count
            || !eventKeys.SetEquals(summaryKeys);
    }

    private static string GetHistoryEventIdentityKey(LaunchHistoryEvent historyEvent)
    {
        return CreateIdentityKey(
            historyEvent.ProcessName,
            historyEvent.Architecture,
            historyEvent.ExecutablePath,
            historyEvent.PackageIdentity,
            historyEvent.PublisherIdentity);
    }

    private static string GetSummaryIdentityKey(LaunchHistorySummary summary)
    {
        return CreateIdentityKey(
            summary.ProcessName,
            summary.Architecture,
            summary.LastExecutablePath,
            summary.PackageIdentity,
            summary.PublisherIdentity);
    }

    private static List<LaunchHistorySummary> BuildSummaries(IEnumerable<LaunchHistoryEvent> events)
    {
        return events
            .Where(historyEvent => IsValidEvent(historyEvent, DateTimeOffset.MinValue, DateTimeOffset.MaxValue))
            .GroupBy(
                GetHistoryEventIdentityKey,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => IgnoredProcessFilter.NormalizeName(group.First().ProcessName).Length > 0)
            .Select(group =>
            {
                LaunchHistoryEvent firstEvent = group.MinBy(historyEvent => historyEvent.DetectedAt)!;
                LaunchHistoryEvent lastEvent = group.MaxBy(historyEvent => historyEvent.DetectedAt)!;

                return new LaunchHistorySummary(
                    firstEvent.ProcessName,
                    firstEvent.Architecture,
                    group.Count(),
                    firstEvent.DetectedAt,
                    lastEvent.DetectedAt,
                    lastEvent.ExecutablePath,
                    lastEvent.ProcessId,
                    lastEvent.PackageIdentity,
                    lastEvent.PublisherIdentity);
            })
            .OrderByDescending(summary => summary.LastSeenAt)
            .ThenBy(summary => summary.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<LaunchHistorySummary> ApplyIgnoredNames(
        IEnumerable<LaunchHistorySummary> summaries,
        IReadOnlySet<string> ignoredNames)
    {
        HashSet<string> normalizedIgnoredNames = ignoredNames
            .Select(IgnoredProcessFilter.NormalizeName)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return summaries
            .Select(summary => summary with
            {
                IsIgnored = normalizedIgnoredNames.Contains(IgnoredProcessFilter.NormalizeName(summary.ProcessName))
            })
            .ToList();
    }

    private static bool IsValidEvent(
        LaunchHistoryEvent? historyEvent,
        DateTimeOffset cutoff,
        DateTimeOffset now)
    {
        return historyEvent is not null
            && IgnoredProcessFilter.NormalizeName(historyEvent.ProcessName).Length > 0
            && !string.IsNullOrWhiteSpace(historyEvent.Architecture)
            && historyEvent.DetectedAt != default
            && historyEvent.DetectedAt >= cutoff
            && historyEvent.DetectedAt <= now;
    }

    private static bool IsValidSummary(
        LaunchHistorySummary? summary,
        DateTimeOffset cutoff,
        DateTimeOffset now)
    {
        return summary is not null
            && IgnoredProcessFilter.NormalizeName(summary.ProcessName).Length > 0
            && !string.IsNullOrWhiteSpace(summary.Architecture)
            && summary.LaunchCount > 0
            && summary.FirstSeenAt != default
            && summary.LastSeenAt != default
            && summary.LastSeenAt >= cutoff
            && summary.LastSeenAt <= now;
    }

    private static string NormalizeIdentityPart(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string CreateIdentityKey(
        string processName,
        string architecture,
        string? executablePath,
        string? packageIdentity,
        string? publisherIdentity)
    {
        return string.Concat(
            IgnoredProcessFilter.NormalizeName(processName),
            '\u001f',
            architecture.Trim(),
            '\u001f',
            NormalizeIdentityPart(executablePath),
            '\u001f',
            NormalizeIdentityPart(packageIdentity),
            '\u001f',
            NormalizeIdentityPart(publisherIdentity));
    }

    private async Task WriteEventsAsync(
        IReadOnlyList<LaunchHistoryEvent> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            DeleteIfExists(eventsFilePath);
            return;
        }

        string? directory = Path.GetDirectoryName(eventsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        IEnumerable<string> lines = events.Select(historyEvent => JsonSerializer.Serialize(
            historyEvent,
            LaunchHistoryJsonContext.Default.LaunchHistoryEvent));

        await File.WriteAllLinesAsync(eventsFilePath, lines, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteSummaryAsync(
        IReadOnlyList<LaunchHistorySummary> summaries,
        CancellationToken cancellationToken)
    {
        if (summaries.Count == 0)
        {
            DeleteIfExists(summaryFilePath);
            return;
        }

        string? directory = Path.GetDirectoryName(summaryFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(summaryFilePath);
        await JsonSerializer.SerializeAsync(
                stream,
                summaries.ToArray(),
                LaunchHistoryJsonContext.Default.LaunchHistorySummaryArray,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LaunchHistoryEvent))]
[JsonSerializable(typeof(LaunchHistoryEvent[]))]
[JsonSerializable(typeof(LaunchHistorySummary[]))]
internal sealed partial class LaunchHistoryJsonContext : JsonSerializerContext;
