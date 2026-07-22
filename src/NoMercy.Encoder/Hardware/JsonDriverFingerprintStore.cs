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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.Hardware;

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
            Path.GetDirectoryName(path: options.SpeedIndexCachePath ?? "speed_index.json")
            ?? Path.GetTempPath();

        return Path.Combine(path1: dir, path2: "driver_fingerprint.json");
    }

    public Task<string?> LoadHashAsync(CancellationToken ct = default)
    {
        string path = ResolvePath();

        if (!storage.Exists(path: path))
            return Task.FromResult<string?>(result: null);

        try
        {
            string json = Encoding.UTF8.GetString(bytes: storage.Read(path: path));
            FingerprintDto? dto = JsonConvert.DeserializeObject<FingerprintDto>(value: json);
            string? hash = dto?.Hash is { Length: > 0 } h ? h : null;

            return Task.FromResult(result: hash);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Could not load driver fingerprint at {Path} — treating as missing",
                args: path
            );
            return Task.FromResult<string?>(result: null);
        }
    }

    public Task SaveHashAsync(string hash, CancellationToken ct = default)
    {
        string path = ResolvePath();

        try
        {
            string? dir = Path.GetDirectoryName(path: path);
            if (!string.IsNullOrWhiteSpace(value: dir))
                storage.CreateDirectory(path: dir);

            FingerprintDto dto = new(Hash: hash);
            string tmp = path + ".tmp";
            storage.Write(
                path: tmp,
                bytes: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: dto, formatting: Formatting.Indented))
            );
            if (storage.Exists(path: path))
                storage.Delete(path: path);
            storage.Move(from: tmp, to: path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(exception: ex, message: "Could not save driver fingerprint to {Path}", args: path);
        }

        return Task.CompletedTask;
    }

    private sealed record FingerprintDto([property: JsonProperty(propertyName: "hash")] string Hash);
}
