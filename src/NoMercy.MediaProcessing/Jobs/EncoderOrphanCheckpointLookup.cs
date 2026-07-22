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

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Jobs;
using NoMercy.Queue.MediaServer;

namespace NoMercy.MediaProcessing.Jobs;

/// <summary>
/// Looks up crash checkpoints for encoder queue jobs during orphan recovery.
/// Extracts the output directory from the serialized job payload, then
/// delegates to <see cref="ICheckpointStore"/> to check for a crash checkpoint.
/// </summary>
public class EncoderOrphanCheckpointLookup(
    ICheckpointStore checkpointStore,
    ILogger<EncoderOrphanCheckpointLookup> logger
) : IOrphanCheckpointLookup
{
    public async Task<bool> HasCheckpointAsync(string jobPayload, CancellationToken ct = default)
    {
        string? outputDirectory = ExtractOutputDirectory(payload: jobPayload);
        if (string.IsNullOrEmpty(value: outputDirectory))
            return false;

        try
        {
            JobCheckpoint? checkpoint = await checkpointStore.LoadAsync(outputDirectory: outputDirectory, ct: ct);
            return checkpoint?.FailedAt is not null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to load checkpoint for OutputDirectory={OutputDirectory}",
                args: outputDirectory
            );
            return false;
        }
    }

    private static string? ExtractOutputDirectory(string payload)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json: payload);
            JsonElement root = doc.RootElement;

            if (
                root.TryGetProperty(propertyName: "OutputDirectory", value: out JsonElement dirElement)
                && dirElement.ValueKind == JsonValueKind.String
            )
            {
                return dirElement.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
