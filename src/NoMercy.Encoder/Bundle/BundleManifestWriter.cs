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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public class BundleManifestWriter : IBundleManifestWriter
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    public async Task WriteAsync(
        IStorage storage,
        string path,
        BundleManifest manifest,
        CancellationToken ct
    )
    {
        string json = JsonConvert.SerializeObject(manifest, Settings);
        await storage.WriteAsync(path, Encoding.UTF8.GetBytes(json), ct);
    }

    public async Task<BundleManifest?> ReadAsync(
        IStorage storage,
        string path,
        CancellationToken ct
    )
    {
        if (!storage.Exists(path))
            return null;
        byte[] bytes = await storage.ReadAsync(path, ct);
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<BundleManifest>(json, Settings);
    }

    public Task<ReconcileReport> ReconcileAsync(
        IStorage storage,
        string bundleDirectory,
        BundleManifest manifest,
        CancellationToken ct
    )
    {
        IReadOnlyList<StorageEntry> onDisk = storage.List(bundleDirectory, "*", recursive: true);

        string dirPrefix = bundleDirectory.TrimEnd('/') + "/";

        HashSet<string> diskRel = new(StringComparer.OrdinalIgnoreCase);
        foreach (StorageEntry entry in onDisk)
        {
            if (entry.IsDirectory)
                continue;
            string rel = entry.Path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase)
                ? entry.Path[dirPrefix.Length..]
                : entry.Path;
            diskRel.Add(rel);
        }

        HashSet<string> manifestSet = new(manifest.Files, StringComparer.OrdinalIgnoreCase);

        // Pass the comparer explicitly — Enumerable.Except ignores the source
        // HashSet's custom comparer and would do an ordinal-case-sensitive
        // diff, mass-flagging mixed-case filenames as extra+missing.
        List<string> extra = [.. diskRel.Except(manifestSet, StringComparer.OrdinalIgnoreCase)];
        List<string> missing = [.. manifestSet.Except(diskRel, StringComparer.OrdinalIgnoreCase)];

        return Task.FromResult(new ReconcileReport(extra, missing));
    }
}
