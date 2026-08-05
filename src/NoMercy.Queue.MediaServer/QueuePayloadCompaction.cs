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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercyQueue.Core;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Rewrites queue rows written before job payloads carried references instead of
/// copies, so an existing backlog survives the change instead of being skipped.
///
/// <para>A music encode used to serialize the whole MusicBrainz release into every
/// track's payload. Those rows deserialize into the current job shape with no
/// release id and no destination path, which would make each one a no-op — a
/// silent way to lose a library's worth of queued encodes. This lifts the release
/// out to the shared store and rewrites the row to point at it, so the work is
/// preserved and the space is returned.</para>
///
/// <para>Unmigrated rows are exactly the rows with no payload hash, which is an
/// indexed lookup, so this is idempotent and costs nothing once it has run.</para>
/// </summary>
public class QueuePayloadCompaction(
    IDbContextFactory<QueueContext> contextFactory,
    IQueueJobBlobStore blobStore,
    ILogger<QueuePayloadCompaction> logger
)
{
    // Rows are read whole, and an unmigrated music payload is about a megabyte,
    // so the batch is what bounds peak memory.
    private const int BatchSize = 50;

    private const string MusicEncodeJobType = "MusicEncodeJob";
    private const string MusicMetadataJobType = "MusicMetadataJob";

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        int migrated = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using QueueContext context = await contextFactory.CreateDbContextAsync(
                cancellationToken
            );

            List<QueueJob> batch = await context
                .QueueJobs.Where(job => job.PayloadHash == string.Empty)
                .OrderBy(job => job.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            foreach (QueueJob job in batch)
            {
                job.Payload = await CompactAsync(job.Payload);
                job.PayloadHash = QueuePayloadHash.For(job.Payload);
                job.SharedInputKey = SharedInputKeyOf(job.Payload);
            }

            await context.SaveChangesAsync(cancellationToken);

            migrated += batch.Count;
            logger.LogInformation("Compacted {Migrated} queue payloads so far", migrated);
        }

        if (migrated > 0)
            logger.LogInformation("Queue payload compaction finished: {Migrated} rows", migrated);

        return migrated;
    }

    /// <summary>
    /// The slim form of one payload, or the payload unchanged when it is already
    /// slim or is a kind this does not rewrite. A payload that cannot be read is
    /// returned untouched — it is not this pass's job to decide it is rubbish.
    /// </summary>
    private async Task<string> CompactAsync(string payload)
    {
        JObject parsed;
        try
        {
            parsed = JObject.Parse(payload);
        }
        catch (JsonException)
        {
            return payload;
        }

        string type = parsed.Value<string>("$type") ?? string.Empty;

        if (type.Contains(MusicEncodeJobType, StringComparison.Ordinal))
            return await CompactMusicEncodeAsync(parsed) ?? payload;

        if (type.Contains(MusicMetadataJobType, StringComparison.Ordinal))
            return CompactMusicMetadata(parsed) ?? payload;

        return payload;
    }

    private async Task<string?> CompactMusicEncodeAsync(JObject parsed)
    {
        if (parsed["folderMetaData"] is not JObject folderMetaData)
            return null;

        if (folderMetaData["musicBrainzRelease"] is not JObject release)
            return null;

        Guid releaseId = GuidOf(release["id"]);
        Guid trackId = GuidOf(parsed["foundTrack"]?["id"]);

        // Without both ids the row cannot name its own work, and inventing either
        // would encode one track's audio under another track's name.
        if (releaseId == Guid.Empty || trackId == Guid.Empty)
            return null;

        await blobStore.WriteAsync(
            SharedInputKeys.Release(releaseId),
            release.ToString(Formatting.None)
        );

        parsed["releaseId"] = releaseId;
        parsed["trackId"] = trackId;
        parsed["basePath"] = folderMetaData.Value<string>("basePath") ?? string.Empty;
        parsed["artistName"] = folderMetaData.Value<string>("artistName") ?? string.Empty;
        parsed["releaseName"] = folderMetaData.Value<string>("releaseName") ?? string.Empty;
        parsed["year"] = folderMetaData.Value<int?>("year") ?? 0;

        parsed.Remove("folderMetaData");
        parsed.Remove("foundTrack");
        parsed.Remove("mediaFile");

        return parsed.ToString(Formatting.None);
    }

    private static string? CompactMusicMetadata(JObject parsed)
    {
        JObject? artist = parsed["musicBrainzArtist"] as JObject;
        JObject? releaseGroup = parsed["musicBrainzReleaseGroup"] as JObject;

        if (artist is null && releaseGroup is null)
            return null;

        if (artist is not null)
            parsed["artistId"] = GuidOf(artist["id"]);

        if (releaseGroup is not null)
            parsed["releaseGroupId"] = GuidOf(releaseGroup["id"]);

        parsed.Remove("musicBrainzArtist");
        parsed.Remove("musicBrainzReleaseGroup");

        return parsed.ToString(Formatting.None);
    }

    private static string? SharedInputKeyOf(string payload)
    {
        try
        {
            Guid releaseId = GuidOf(JObject.Parse(payload)["releaseId"]);
            return releaseId == Guid.Empty ? null : SharedInputKeys.Release(releaseId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ids arrive as JSON strings, which JToken will not convert to a Guid on its
    /// own — asking it to throws rather than returning null.
    /// </summary>
    private static Guid GuidOf(JToken? token)
    {
        return Guid.TryParse(token?.Value<string>(), out Guid parsed) ? parsed : Guid.Empty;
    }
}
