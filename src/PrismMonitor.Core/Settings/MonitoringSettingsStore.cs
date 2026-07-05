using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrismMonitor.Core.Settings;

public sealed class MonitoringSettingsStore(string filePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<MonitoringSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(MonitoringSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                MonitoringSettingsJsonContext.Default.MonitoringSettings,
                cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<MonitoringSettings> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return MonitoringSettings.Default;
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            MonitoringSettings? settings = await JsonSerializer.DeserializeAsync(
                stream,
                MonitoringSettingsJsonContext.Default.MonitoringSettings,
                cancellationToken)
                .ConfigureAwait(false);

            return settings ?? MonitoringSettings.Default;
        }
        catch (JsonException)
        {
            return MonitoringSettings.Default;
        }
        catch (IOException)
        {
            return MonitoringSettings.Default;
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MonitoringSettings))]
internal sealed partial class MonitoringSettingsJsonContext : JsonSerializerContext;
