using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Persists the driver fingerprint hash in the application database
/// (<see cref="AppDbContext.Configuration"/>) keyed by
/// <see cref="ConfigKey"/>. Replaces the file-based
/// <see cref="JsonDriverFingerprintStore"/> so server admins can move /
/// back up the install without leaving a stray
/// <c>driver_fingerprint.json</c> in the service directory.
///
/// On first read, if the legacy JSON file is still present, its hash is
/// imported into the database and the file is deleted — gives existing
/// installs a one-shot migration without operator action.
/// </summary>
public class DbDriverFingerprintStore(
    EncoderOptions options,
    ILogger<DbDriverFingerprintStore> logger,
    IStorage storage
) : IDriverFingerprintStore
{
    public const string ConfigKey = "encoder.driver_fingerprint";

    private string LegacyJsonPath()
    {
        string dir =
            Path.GetDirectoryName(options.SpeedIndexCachePath ?? "speed_index.json")
            ?? Path.GetTempPath();
        return Path.Combine(dir, "driver_fingerprint.json");
    }

    public async Task<string?> LoadHashAsync(CancellationToken ct = default)
    {
        try
        {
            await using AppDbContext db = new();
            Configuration? row = await db
                .Configuration.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Key == ConfigKey, ct);

            string? hash = row?.Value is { Length: > 0 } v ? v : null;

            // One-shot migration: import any legacy JSON file the first time
            // through, then delete it so the file never reappears.
            if (hash is null)
            {
                hash = await TryImportLegacyJsonAsync(db, ct);
            }

            return hash;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not load driver fingerprint from AppDbContext.Configuration — treating as missing"
            );
            return null;
        }
    }

    public async Task SaveHashAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            await using AppDbContext db = new();
            Configuration? row = await db.Configuration.FirstOrDefaultAsync(
                c => c.Key == ConfigKey,
                ct
            );
            if (row is null)
            {
                db.Configuration.Add(new() { Key = ConfigKey, Value = hash });
            }
            else
            {
                row.Value = hash;
                db.Configuration.Update(row);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not save driver fingerprint to AppDbContext.Configuration"
            );
        }
    }

    private async Task<string?> TryImportLegacyJsonAsync(AppDbContext db, CancellationToken ct)
    {
        string legacy = LegacyJsonPath();
        if (!storage.Exists(legacy))
            return null;

        try
        {
            string json = Encoding.UTF8.GetString(storage.Read(legacy));
            FingerprintDto? dto = JsonConvert.DeserializeObject<FingerprintDto>(json);
            string? hash = dto?.Hash is { Length: > 0 } h ? h : null;
            if (hash is null)
                return null;

            db.Configuration.Add(new() { Key = ConfigKey, Value = hash });
            await db.SaveChangesAsync(ct);

            // Delete the file only after the row commits — losing the file
            // before the DB write would leave the install fingerprint-less.
            try
            {
                storage.Delete(legacy);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    ex,
                    "Imported legacy driver_fingerprint.json but could not delete the file at {Path}",
                    legacy
                );
            }

            logger.LogInformation(
                "Migrated legacy driver_fingerprint.json into AppDbContext.Configuration"
            );

            return hash;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Legacy driver_fingerprint.json at {Path} could not be imported — ignoring",
                legacy
            );
            return null;
        }
    }

    private sealed record FingerprintDto([property: JsonProperty("hash")] string Hash);
}
