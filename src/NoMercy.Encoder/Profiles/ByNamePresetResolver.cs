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

using Newtonsoft.Json;
using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Profiles;

public record PresetResolveRequest(string Name, string ProfileJson, string? ParentName = null);

/// <summary>
/// Name-based preset lookup used by the dashboard controllers to walk a
/// parent chain via human-readable names. Distinct from <see cref="IPresetLookup"/>,
/// which keys by <see cref="Ulid"/> for the canonical resolver. Once the
/// dashboard moves fully to id-based inheritance, this interface and its
/// by-name resolver can be retired.
/// </summary>
public interface INamePresetLookup
{
    /// <summary>Returns the preset source by name, or null when unknown.</summary>
    PresetResolveRequest? FindByName(string name);
}

public interface INamePresetResolver
{
    /// <summary>
    /// Walks the preset's inheritance chain and returns the effective
    /// <see cref="EncodingProfile"/>. Parent preset JSON is the base; the
    /// child's JSON is deserialized on top, so any field the child specifies
    /// overrides the parent. Throws on cycles or missing parents.
    /// </summary>
    EncodingProfile Resolve(PresetResolveRequest request, INamePresetLookup lookup);
}

public class ByNamePresetResolver : INamePresetResolver
{
    private const int MaxInheritanceDepth = 10;

    public EncodingProfile Resolve(PresetResolveRequest request, INamePresetLookup lookup)
    {
        // Top of the chain first, then walk down applying child overrides.
        List<PresetResolveRequest> chain = BuildChain(leaf: request, lookup: lookup);

        // Start with the root preset as the base profile.
        EncodingProfile profile = DeserializeProfile(request: chain[index: 0]);

        // Overlay each descendant. Record-level replacement; preset authors
        // who want to keep a parent field simply omit it from their JSON.
        // We achieve this by deserializing the child JSON into a populated
        // instance of the parent profile — Newtonsoft leaves unmentioned
        // fields untouched.
        for (int i = 1; i < chain.Count; i++)
        {
            JsonConvert.PopulateObject(value: chain[index: i].ProfileJson, target: profile);
        }

        return profile;
    }

    private static List<PresetResolveRequest> BuildChain(
        PresetResolveRequest leaf,
        INamePresetLookup lookup
    )
    {
        List<PresetResolveRequest> reversed = [leaf];
        HashSet<string> seen = new(comparer: StringComparer.OrdinalIgnoreCase) { leaf.Name };

        PresetResolveRequest current = leaf;
        while (current.ParentName is string parent)
        {
            if (!seen.Add(item: parent))
                throw new InvalidOperationException(
                    message: $"[{EncoderRuleId.ParentIdCycle}] Preset inheritance cycle detected at '{parent}'"
                );

            if (reversed.Count >= MaxInheritanceDepth)
                throw new InvalidOperationException(
                    message: $"Preset inheritance chain exceeds {MaxInheritanceDepth} levels"
                );

            PresetResolveRequest? parentPreset =
                lookup.FindByName(name: parent)
                ?? throw new InvalidOperationException(message: $"Parent preset '{parent}' not found");

            reversed.Add(item: parentPreset);
            current = parentPreset;
        }

        // Root first (reversed.Last()), then children down to the leaf.
        reversed.Reverse();
        return reversed;
    }

    private static EncodingProfile DeserializeProfile(PresetResolveRequest request)
    {
        EncodingProfile? profile = JsonConvert.DeserializeObject<EncodingProfile>(
            value: request.ProfileJson
        );

        if (profile is null)
            throw new InvalidOperationException(
                message: $"Preset '{request.Name}' has an empty or invalid ProfileJson"
            );

        return profile;
    }
}
