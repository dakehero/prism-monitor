using System.Text.Json;

namespace NativeGuard.Core.Processes;

public sealed class IgnoredProcessStore(string filePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<string>> GetIgnoredNamesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadNamesAsync(cancellationToken).ConfigureAwait(false);
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
            List<string> names = await ReadNamesAsync(cancellationToken).ConfigureAwait(false);
            if (names.Any(name => string.Equals(
                    IgnoredProcessFilter.NormalizeName(name),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            names.Add(normalizedName);
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
            List<string> names = await ReadNamesAsync(cancellationToken).ConfigureAwait(false);
            names.RemoveAll(name => string.Equals(
                IgnoredProcessFilter.NormalizeName(name),
                normalizedName,
                StringComparison.OrdinalIgnoreCase));

            await WriteNamesAsync(names, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<string>> ReadNamesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        string[]? names = await JsonSerializer.DeserializeAsync<string[]>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return NormalizeAndSort(names ?? []);
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
        await JsonSerializer.SerializeAsync(stream, normalizedNames, cancellationToken: cancellationToken)
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
}
