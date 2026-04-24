namespace NoMercy.Encoder.Hardware;

using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

/// <summary>
/// Persists the driver fingerprint hash to a JSON file alongside the SpeedIndex cache.
/// Corrupt or missing files are treated as "no previous hash" so the caller simply
/// treats the next boot as a first boot rather than crashing.
/// </summary>
public class JsonDriverFingerprintStore(
    EncoderOptions options,
    ILogger<JsonDriverFingerprintStore> logger,
    IStorage storage
) : IDriverFingerprintStore
{
    private string ResolvePath()
    {
        string dir =
            Path.GetDirectoryName(options.SpeedIndexCachePath ?? "speed_index.json")
            ?? Path.GetTempPath();

        return Path.Combine(dir, "driver_fingerprint.json");
    }

    public Task<string?> LoadHashAsync(CancellationToken ct = default)
    {
        string path = ResolvePath();

        if (!storage.Exists(path))
            return Task.FromResult<string?>(null);

        try
        {
            string json = Encoding.UTF8.GetString(storage.Read(path));
            FingerprintDto? dto = JsonConvert.DeserializeObject<FingerprintDto>(json);
            string? hash = dto?.Hash is { Length: > 0 } h ? h : null;

            return Task.FromResult(hash);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not load driver fingerprint at {Path} — treating as missing",
                path
            );
            return Task.FromResult<string?>(null);
        }
    }

    public Task SaveHashAsync(string hash, CancellationToken ct = default)
    {
        string path = ResolvePath();

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                storage.CreateDirectory(dir);

            FingerprintDto dto = new(hash);
            string tmp = path + ".tmp";
            storage.Write(
                tmp,
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto, Formatting.Indented))
            );
            if (storage.Exists(path))
                storage.Delete(path);
            storage.Move(tmp, path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not save driver fingerprint to {Path}", path);
        }

        return Task.CompletedTask;
    }

    private sealed record FingerprintDto([property: JsonProperty("hash")] string Hash);
}
