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

namespace NoMercy.Encoder.Profiles;

using Newtonsoft.Json.Linq;

public static class DiskOverlayLoader
{
    public record LoadedPreset(
        EncodingProfile Profile,
        Ulid? ParentPresetId,
        string? Description,
        string SourcePath
    );

    public record LoadResult(IReadOnlyList<LoadedPreset> Loaded, IReadOnlyList<string> Errors);

    public static LoadResult Load(string directory)
    {
        if (!Directory.Exists(path: directory))
            return new(Loaded: [], Errors: []);

        List<LoadedPreset> loaded = [];
        List<string> errors = [];
        HashSet<Ulid> seenIds = [];

        foreach (string path in Directory.EnumerateFiles(path: directory, searchPattern: "*.json"))
        {
            try
            {
                string contents = File.ReadAllText(path: path);
                JObject root = JObject.Parse(json: contents);

                EncodingProfile profile;
                Ulid? parentId = null;
                string? description = null;

                if (root[propertyName: "profile"] is JObject inner)
                {
                    profile =
                        inner.ToObject<EncodingProfile>()
                        ?? throw new InvalidOperationException(
                            message: "profile object failed to deserialize"
                        );
                    parentId = root[propertyName: "parentPresetId"]?.Value<string>() is { Length: > 0 } pid
                        ? Ulid.Parse(base32: pid)
                        : null;
                    description = root[propertyName: "description"]?.Value<string>();
                }
                else
                {
                    profile =
                        root.ToObject<EncodingProfile>()
                        ?? throw new InvalidOperationException(message: "profile failed to deserialize");
                }

                if (!seenIds.Add(item: profile.Id))
                    errors.Add(item: $"{path}: duplicate Ulid {profile.Id} (later file wins)");

                loaded.Add(item: new(Profile: profile, ParentPresetId: parentId, Description: description, SourcePath: path));
            }
            catch (Exception ex)
            {
                errors.Add(item: $"{path}: {ex.Message}");
            }
        }

        return new(Loaded: loaded, Errors: errors);
    }
}
