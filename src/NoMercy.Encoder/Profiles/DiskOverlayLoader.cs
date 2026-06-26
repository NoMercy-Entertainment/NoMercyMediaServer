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
        if (!Directory.Exists(directory))
            return new([], []);

        List<LoadedPreset> loaded = [];
        List<string> errors = [];
        HashSet<Ulid> seenIds = [];

        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                string contents = File.ReadAllText(path);
                JObject root = JObject.Parse(contents);

                EncodingProfile profile;
                Ulid? parentId = null;
                string? description = null;

                if (root["profile"] is JObject inner)
                {
                    profile =
                        inner.ToObject<EncodingProfile>()
                        ?? throw new InvalidOperationException(
                            "profile object failed to deserialize"
                        );
                    parentId = root["parentPresetId"]?.Value<string>() is { Length: > 0 } pid
                        ? Ulid.Parse(pid)
                        : null;
                    description = root["description"]?.Value<string>();
                }
                else
                {
                    profile =
                        root.ToObject<EncodingProfile>()
                        ?? throw new InvalidOperationException("profile failed to deserialize");
                }

                if (!seenIds.Add(profile.Id))
                    errors.Add($"{path}: duplicate Ulid {profile.Id} (later file wins)");

                loaded.Add(new(profile, parentId, description, path));
            }
            catch (Exception ex)
            {
                errors.Add($"{path}: {ex.Message}");
            }
        }

        return new(loaded, errors);
    }
}
