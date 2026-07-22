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

using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Profiles;

public static class PresetResolver
{
    private const int MaxDepth = 8;
    private const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Test seam — production startup leaves this empty.
    /// Inject migrations here in tests to verify upgrade paths.
    /// </summary>
    internal static IReadOnlyList<IProfileMigration> Migrations { get; set; } = [];

    private static JObject EnsureCurrent(JObject input)
    {
        int version = input[propertyName: "schemaVersion"]?.Value<int>() ?? CurrentSchemaVersion;
        while (version < CurrentSchemaVersion)
        {
            IProfileMigration? step = Migrations.FirstOrDefault(predicate: m => m.FromVersion == version);
            if (step is null)
                throw new InvalidOperationException(message: $"No migration from schema v{version}.");
            input = step.Migrate(input: input);
            version = step.ToVersion;
        }
        return input;
    }

    public static EncodingProfile Resolve(Ulid presetId, IPresetLookup lookup)
    {
        List<(Ulid Id, string Json)> chain = [];
        HashSet<Ulid> visited = [];
        Ulid? cursor = presetId;

        while (cursor.HasValue)
        {
            if (!visited.Add(item: cursor.Value))
                throw new InvalidOperationException(
                    message: $"Inheritance cycle detected at preset {cursor.Value}."
                );

            if (chain.Count >= MaxDepth)
                throw new InvalidOperationException(
                    message: $"Inheritance chain exceeds max depth of {MaxDepth}."
                );

            (string ProfileJson, Ulid? ParentPresetId)? entry = lookup.Get(presetId: cursor.Value);
            if (entry is null)
                throw new InvalidOperationException(message: $"Preset {cursor.Value} not found in lookup.");

            chain.Add(item: (cursor.Value, entry.Value.ProfileJson));
            cursor = entry.Value.ParentPresetId;
        }

        chain.Reverse();
        JObject accumulator = EnsureCurrent(input: JObject.Parse(json: chain[index: 0].Json));
        for (int i = 1; i < chain.Count; i++)
        {
            JObject child = EnsureCurrent(input: JObject.Parse(json: chain[index: i].Json));
            accumulator.Merge(
                content: child,
                settings: new()
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Merge,
                }
            );
        }

        EncodingProfile? resolved = accumulator.ToObject<EncodingProfile>();
        if (resolved is null)
            throw new InvalidOperationException(message: "Resolved profile failed to deserialize.");
        return resolved;
    }
}
