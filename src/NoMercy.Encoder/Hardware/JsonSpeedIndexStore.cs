namespace NoMercy.Encoder.Hardware;

using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

/// <summary>
/// Persists <see cref="SpeedIndex"/> measurements to a single JSON file at
/// <see cref="EncoderOptions.SpeedIndexCachePath"/>. Corrupt / missing files
/// are logged and treated as "no cache" so the benchmark simply recalibrates.
/// </summary>
public class JsonSpeedIndexStore(
    EncoderOptions options,
    ILogger<JsonSpeedIndexStore> logger,
    IStorage storage
) : ISpeedIndexStore
{
    private DateTime? _lastCalibratedAt;

    public DateTime? LastCalibratedAt => _lastCalibratedAt;

    public SpeedIndex? Load()
    {
        string? path = options.SpeedIndexCachePath;
        if (string.IsNullOrWhiteSpace(path) || !storage.Exists(path))
            return null;

        try
        {
            string json = Encoding.UTF8.GetString(storage.Read(path));
            SpeedIndexDto? dto = JsonConvert.DeserializeObject<SpeedIndexDto>(json);
            if (dto is null)
                return null;

            Dictionary<SpeedKey, SpeedMeasurement> map = new();
            foreach (SpeedEntryDto entry in dto.Entries)
            {
                map[new(entry.Codec, entry.Encoder, entry.Width, entry.DeviceName)] = new(
                    entry.Fps,
                    entry.SpeedMultiplier,
                    entry.MeasuredAt
                );
            }

            _lastCalibratedAt = dto.CalibratedAt;
            return new(map);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not load SpeedIndex cache at {Path} — treating as empty",
                path
            );
            return null;
        }
    }

    public void Save(SpeedIndex index)
    {
        string? path = options.SpeedIndexCachePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogDebug("No SpeedIndexCachePath configured — skipping save");
            return;
        }

        try
        {
            storage.CreateDirectory(Path.GetDirectoryName(path)!);

            DateTime now = DateTime.UtcNow;
            SpeedIndexDto dto = new(
                CalibratedAt: now,
                Entries: index
                    .Measurements.Select(kvp => new SpeedEntryDto(
                        Codec: kvp.Key.Codec,
                        Encoder: kvp.Key.Encoder,
                        Width: kvp.Key.Width,
                        DeviceName: kvp.Key.DeviceName,
                        Fps: kvp.Value.Fps,
                        SpeedMultiplier: kvp.Value.SpeedMultiplier,
                        MeasuredAt: kvp.Value.MeasuredAt
                    ))
                    .ToArray()
            );

            string tmp = path + ".tmp";
            storage.Write(
                tmp,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto, Formatting.Indented))
            );
            if (storage.Exists(path))
                storage.Delete(path);
            storage.Move(tmp, path);

            _lastCalibratedAt = now;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save SpeedIndex cache to {Path}", path);
        }
    }

    private sealed record SpeedIndexDto(DateTime CalibratedAt, SpeedEntryDto[] Entries);

    private sealed record SpeedEntryDto(
        VideoCodecType Codec,
        string Encoder,
        int Width,
        string? DeviceName,
        double Fps,
        double SpeedMultiplier,
        DateTime MeasuredAt
    );
}
