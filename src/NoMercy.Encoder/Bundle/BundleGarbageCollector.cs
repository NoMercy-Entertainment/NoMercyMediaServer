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
using NoMercy.Database;
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public class BundleGarbageCollector(
    IStorage storage,
    IDbContextFactory<MediaContext> contextFactory,
    IBundleManifestWriter manifests,
    ILogger<BundleGarbageCollector> logger
) : IBundleGarbageCollector
{
    private const string EncodesSegment = "/encodes/";

    public async Task<IReadOnlyList<BundleOrphan>> SweepAsync(
        string libraryRoot,
        CancellationToken ct
    )
    {
        List<BundleOrphan> orphans = [];

        // List every file under the library root recursively.
        IReadOnlyList<StorageEntry> allFiles = storage.List(
            libraryRoot,
            pattern: null,
            recursive: true
        );

        if (allFiles.Count == 0)
            return orphans;

        // Collect all *.json files that live directly inside an encodes/{slug}/ directory.
        // "Directly inside" means: after the slug segment there is no further '/'.
        // e.g. library/encodes/web-1080p/manifest.json   → matches
        //      library/encodes/web-1080p/video/foo.json  → skip (sub-directory)
        Dictionary<string, List<StorageEntry>> jsonByBundleDir = [];

        string rootPrefix = libraryRoot.TrimEnd('/') + EncodesSegment;

        foreach (StorageEntry entry in allFiles)
        {
            if (entry.IsDirectory)
                continue;
            if (!entry.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            // Must be under libraryRoot/encodes/
            int encodesStart = entry.Path.IndexOf(
                EncodesSegment,
                StringComparison.OrdinalIgnoreCase
            );
            if (encodesStart < 0)
                continue;

            // After /encodes/ there must be exactly one more path segment
            // (the slug) before the filename — no further slashes.
            int slugStart = encodesStart + EncodesSegment.Length;
            int slugEnd = entry.Path.IndexOf('/', slugStart);
            if (slugEnd < 0)
                continue; // no slash after slug means the file IS the slug directory entry — skip

            string bundleDir = entry.Path[..slugEnd];
            string remainder = entry.Path[(slugEnd + 1)..];

            // Only files directly in the bundle dir, not in sub-dirs.
            if (remainder.Contains('/'))
                continue;

            if (!jsonByBundleDir.TryGetValue(bundleDir, out List<StorageEntry>? list))
            {
                list = [];
                jsonByBundleDir[bundleDir] = list;
            }

            list.Add(entry);
        }

        if (jsonByBundleDir.Count == 0)
            return orphans;

        await using MediaContext db = await contextFactory.CreateDbContextAsync(ct);
        HashSet<string> knownPresetIds = db
            .EncodingPresets.AsNoTracking()
            .Select(p => p.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach ((string bundleDir, List<StorageEntry> jsonFiles) in jsonByBundleDir)
        {
            // Extract the slug: last segment of the bundle dir path.
            int slugSlash = bundleDir.LastIndexOf('/');
            string slug = slugSlash >= 0 ? bundleDir[(slugSlash + 1)..] : bundleDir;

            // Duplicate manifest: more than one JSON file at the bundle root.
            if (jsonFiles.Count > 1)
            {
                logger.LogError(
                    "Duplicate manifest files in bundle dir {BundleDir}: {Files}",
                    bundleDir,
                    string.Join(", ", jsonFiles.Select(f => f.Path))
                );
                orphans.Add(
                    new(
                        Path: bundleDir,
                        PresetSlug: slug,
                        PresetId: string.Empty,
                        Reason: "duplicate-manifest"
                    )
                );
                continue;
            }

            // Exactly one JSON file — must be manifest.json.
            string manifestPath = jsonFiles[0].Path;
            BundleManifest? manifest = await manifests.ReadAsync(manifestPath, ct);
            if (manifest is null)
            {
                logger.LogWarning("Could not read manifest at {Path} — skipping", manifestPath);
                continue;
            }

            // Preset existence check.
            if (!knownPresetIds.Contains(manifest.PresetId))
            {
                orphans.Add(
                    new(
                        Path: bundleDir,
                        PresetSlug: manifest.PresetSlug,
                        PresetId: manifest.PresetId,
                        Reason: "preset deleted"
                    )
                );
                continue;
            }

            // Reconcile on-disk files against the manifest file list.
            ReconcileReport report = await manifests.ReconcileAsync(bundleDir, manifest, ct);

            if (report.ExtraFiles.Count > 0)
                logger.LogWarning(
                    "Bundle {BundleDir} has {Count} extra file(s) not listed in the manifest: {Files}. These may be user additions — no action taken.",
                    bundleDir,
                    report.ExtraFiles.Count,
                    string.Join(", ", report.ExtraFiles)
                );

            if (report.MissingFiles.Count > 0)
                logger.LogWarning(
                    "Bundle {BundleDir} is missing {Count} file(s) listed in the manifest: {Files}. Encode may be incomplete — the user can re-encode to repair.",
                    bundleDir,
                    report.MissingFiles.Count,
                    string.Join(", ", report.MissingFiles)
                );
        }

        return orphans;
    }
}
