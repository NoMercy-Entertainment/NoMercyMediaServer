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
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Walks a library root for every per-media-item <c>.nomercy.json</c>
/// blueprint (see <see cref="MediaBlueprintWriter"/>) and flags any
/// <c>encodes[]</c> entry whose preset no longer exists in the database.
/// A leftover pre-blueprint <c>encodes/{slug}/manifest.json</c> file is
/// vestigial and simply does not match the blueprint filename — it is
/// never inspected and never crashes the sweep.
/// </summary>
public class BundleGarbageCollector(
    IStorage storage,
    IDbContextFactory<MediaContext> contextFactory,
    ILogger<BundleGarbageCollector> logger
) : IBundleGarbageCollector
{
    public async Task<IReadOnlyList<BundleOrphan>> SweepAsync(
        string libraryRoot,
        CancellationToken ct
    )
    {
        List<BundleOrphan> orphans = [];

        IReadOnlyList<StorageEntry> allFiles = storage.List(
            path: libraryRoot,
            pattern: null,
            recursive: true
        );

        if (allFiles.Count == 0)
            return orphans;

        List<StorageEntry> blueprintFiles = allFiles
            .Where(predicate: entry =>
                !entry.IsDirectory
                && string.Equals(
                    a: storage.GetName(path: entry.Path),
                    b: MediaBlueprintWriter.FileName,
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        if (blueprintFiles.Count == 0)
            return orphans;

        await using MediaContext db = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        HashSet<string> knownPresetIds = db
            .EncodingPresets.AsNoTracking()
            .Select(selector: p => p.Id.ToString())
            .ToHashSet(comparer: StringComparer.OrdinalIgnoreCase);

        foreach (StorageEntry blueprintFile in blueprintFiles)
        {
            MediaBlueprint? blueprint = await TryReadBlueprintAsync(path: blueprintFile.Path, ct: ct);
            if (blueprint is null)
                continue;

            string mediaFolder = storage.GetParent(path: blueprintFile.Path) ?? libraryRoot;

            foreach (BlueprintEncode encode in blueprint.Encodes)
            {
                if (knownPresetIds.Contains(item: encode.PresetId))
                    continue;

                orphans.Add(
                    item: new(
                        Path: string.IsNullOrEmpty(value: encode.OutputLocation)
                            ? mediaFolder
                            : encode.OutputLocation,
                        PresetSlug: encode.PresetSlug,
                        PresetId: encode.PresetId,
                        Reason: "preset deleted"
                    )
                );
            }
        }

        return orphans;
    }

    private async Task<MediaBlueprint?> TryReadBlueprintAsync(string path, CancellationToken ct)
    {
        try
        {
            byte[] bytes = await storage.ReadAsync(path: path, ct: ct);
            return JsonConvert.DeserializeObject<MediaBlueprint>(value: Encoding.UTF8.GetString(bytes: bytes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                message: "Could not read blueprint at {Path}: {Message} — skipping", args: [path, ex.Message]
            );
            return null;
        }
    }
}
