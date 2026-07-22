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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Storage;

namespace NoMercy.Encoder.Jobs;

/// <summary>
/// Persists job checkpoints as JSON files next to the encode output.
/// File location: {OutputDirectory}/.checkpoint.json — one per output. Resume
/// reads this; on success the caller deletes it.
/// </summary>
public class JsonCheckpointStore(IStorage storage, ILogger<JsonCheckpointStore> logger)
    : ICheckpointStore
{
    private const string FileName = CheckpointFileNames.FileName;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(JobCheckpoint checkpoint, CancellationToken ct = default)
    {
        storage.CreateDirectory(path: checkpoint.OutputDirectory);
        string path = Path.Combine(path1: checkpoint.OutputDirectory, path2: FileName);

        JobCheckpoint toWrite = checkpoint with { LastUpdated = DateTime.UtcNow };
        string json = JsonSerializer.Serialize(value: toWrite, options: SerializerOptions);
        await storage.WriteAsync(path: path, bytes: Encoding.UTF8.GetBytes(s: json), ct: ct);

        logger.LogDebug(message: "Checkpoint saved: {JobId} → {Path}", args: [checkpoint.JobId, path]);
    }

    public async Task<JobCheckpoint?> LoadAsync(
        string outputDirectory,
        CancellationToken ct = default
    )
    {
        string path = Path.Combine(path1: outputDirectory, path2: FileName);
        if (!storage.Exists(path: path))
            return null;

        try
        {
            await using Stream stream = await storage.OpenReadAsync(path: path, ct: ct);
            return await JsonSerializer.DeserializeAsync<JobCheckpoint>(
                utf8Json: stream,
                options: SerializerOptions,
                cancellationToken: ct
            );
        }
        catch (JsonException ex)
        {
            logger.LogWarning(exception: ex, message: "Checkpoint at {Path} is corrupt; treating as missing", args: path);
            return null;
        }
    }

    public Task DeleteAsync(string outputDirectory, CancellationToken ct = default)
    {
        string path = Path.Combine(path1: outputDirectory, path2: FileName);
        if (storage.Exists(path: path))
        {
            storage.Delete(path: path);
            logger.LogDebug(message: "Checkpoint deleted at {Path}", args: path);
        }

        return Task.CompletedTask;
    }
}
