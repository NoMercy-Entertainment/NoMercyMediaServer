// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
    IStorage storage,
    IDbContextFactory<AppDbContext> contextFactory
) : IDriverFingerprintStore
{
    public const string ConfigKey = "encoder.driver_fingerprint";

    private string LegacyJsonPath()
    {
        string dir =
            Path.GetDirectoryName(path: options.SpeedIndexCachePath ?? "speed_index.json")
            ?? Path.GetTempPath();
        return Path.Combine(path1: dir, path2: "driver_fingerprint.json");
    }

    public async Task<string?> LoadHashAsync(CancellationToken ct = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            Configuration? row = await db
                .Configuration.AsNoTracking()
                .FirstOrDefaultAsync(predicate: c => c.Key == ConfigKey, cancellationToken: ct);

            string? hash = row?.Value is { Length: > 0 } v ? v : null;

            // One-shot migration: import any legacy JSON file the first time
            // through, then delete it so the file never reappears.
            if (hash is null)
            {
                hash = await TryImportLegacyJsonAsync(db: db, ct: ct);
            }

            return hash;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Could not load driver fingerprint from AppDbContext.Configuration — treating as missing"
            );
            return null;
        }
    }

    public async Task SaveHashAsync(string hash, CancellationToken ct = default)
    {
        try
        {
            await using AppDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            Configuration? row = await db.Configuration.FirstOrDefaultAsync(
                predicate: c => c.Key == ConfigKey,
                cancellationToken: ct
            );
            if (row is null)
            {
                db.Configuration.Add(entity: new() { Key = ConfigKey, Value = hash });
            }
            else
            {
                row.Value = hash;
                db.Configuration.Update(entity: row);
            }
            await db.SaveChangesAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Could not save driver fingerprint to AppDbContext.Configuration"
            );
        }
    }

    private async Task<string?> TryImportLegacyJsonAsync(AppDbContext db, CancellationToken ct)
    {
        string legacy = LegacyJsonPath();
        if (!storage.Exists(path: legacy))
            return null;

        try
        {
            string json = Encoding.UTF8.GetString(bytes: storage.Read(path: legacy));
            FingerprintDto? dto = JsonConvert.DeserializeObject<FingerprintDto>(value: json);
            string? hash = dto?.Hash is { Length: > 0 } h ? h : null;
            if (hash is null)
                return null;

            db.Configuration.Add(entity: new() { Key = ConfigKey, Value = hash });
            await db.SaveChangesAsync(cancellationToken: ct);

            // Delete the file only after the row commits — losing the file
            // before the DB write would leave the install fingerprint-less.
            try
            {
                storage.Delete(path: legacy);
            }
            catch (Exception ex)
            {
                logger.LogDebug(
                    exception: ex,
                    message: "Imported legacy driver_fingerprint.json but could not delete the file at {Path}",
                    args: legacy
                );
            }

            logger.LogInformation(
                message: "Migrated legacy driver_fingerprint.json into AppDbContext.Configuration"
            );

            return hash;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Legacy driver_fingerprint.json at {Path} could not be imported — ignoring",
                args: legacy
            );
            return null;
        }
    }

    private sealed record FingerprintDto([property: JsonProperty(propertyName: "hash")] string Hash);
}
