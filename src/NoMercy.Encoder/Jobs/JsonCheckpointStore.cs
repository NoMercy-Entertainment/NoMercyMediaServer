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
        storage.CreateDirectory(checkpoint.OutputDirectory);
        string path = Path.Combine(checkpoint.OutputDirectory, FileName);

        JobCheckpoint toWrite = checkpoint with { LastUpdated = DateTime.UtcNow };
        string json = JsonSerializer.Serialize(toWrite, SerializerOptions);
        await storage.WriteAsync(path, Encoding.UTF8.GetBytes(json), ct);

        logger.LogDebug("Checkpoint saved: {JobId} → {Path}", checkpoint.JobId, path);
    }

    public async Task<JobCheckpoint?> LoadAsync(
        string outputDirectory,
        CancellationToken ct = default
    )
    {
        string path = Path.Combine(outputDirectory, FileName);
        if (!storage.Exists(path))
            return null;

        try
        {
            await using Stream stream = await storage.OpenReadAsync(path, ct);
            return await JsonSerializer.DeserializeAsync<JobCheckpoint>(
                stream,
                SerializerOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Checkpoint at {Path} is corrupt; treating as missing", path);
            return null;
        }
    }

    public Task DeleteAsync(string outputDirectory, CancellationToken ct = default)
    {
        string path = Path.Combine(outputDirectory, FileName);
        if (storage.Exists(path))
        {
            storage.Delete(path);
            logger.LogDebug("Checkpoint deleted at {Path}", path);
        }

        return Task.CompletedTask;
    }
}
