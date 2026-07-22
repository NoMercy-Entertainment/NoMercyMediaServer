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

using Database;
using Database.Models.Media;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

public class BuiltinPresetSeeder(MediaContext context)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        EncodingProfile[] builtins = BuiltinPresets.All();
        HashSet<Ulid> builtinIds = builtins.Select(selector: p => p.Id).ToHashSet();

        foreach (EncodingProfile profile in builtins)
        {
            string profileJson = JsonConvert.SerializeObject(value: profile);
            EncodingPreset? existing = await context.EncodingPresets.FirstOrDefaultAsync(
                predicate: p => p.Id == profile.Id,
                cancellationToken: ct
            );

            if (existing is null)
            {
                context.EncodingPresets.Add(
                    entity: new()
                    {
                        Id = profile.Id,
                        Name = profile.Name,
                        Description = profile.Description,
                        ProfileJson = profileJson,
                        IsBuiltIn = true,
                        Source = "builtin",
                    }
                );
            }
            else
            {
                existing.Name = profile.Name;
                existing.Description = profile.Description;
                existing.ProfileJson = profileJson;
                existing.IsBuiltIn = true;
                existing.Source = "builtin";
            }
        }

        await context.SaveChangesAsync(cancellationToken: ct);
        await RetireUnshippedBuiltinsAsync(builtinIds: builtinIds, ct: ct);
    }

    /// <summary>
    /// Deals with built-ins that no longer ship, usually because they were
    /// renamed and a name change mints a new id.
    ///
    /// Deleting them outright is not an option: <c>EncodingPresetFolders</c>
    /// cascades on the preset FK, so dropping a built-in silently takes every
    /// folder link with it and the folder quietly stops encoding. Instead:
    /// redirect the links when <see cref="BuiltinPresetRenames.IdRedirects"/>
    /// names a replacement, keep the preset as a user preset when something
    /// still points at it, and only delete when nothing does.
    /// </summary>
    private async Task RetireUnshippedBuiltinsAsync(HashSet<Ulid> builtinIds, CancellationToken ct)
    {
        List<EncodingPreset> unshipped = await context
            .EncodingPresets.Where(predicate: preset => preset.IsBuiltIn)
            .ToListAsync(cancellationToken: ct);

        List<EncodingPreset> stale = unshipped
            .Where(predicate: preset => !builtinIds.Contains(item: preset.Id))
            .ToList();

        if (stale.Count == 0)
            return;

        foreach (EncodingPreset preset in stale)
        {
            List<EncodingPresetFolder> links = await context
                .EncodingPresetFolders.Where(predicate: link => link.PresetId == preset.Id)
                .ToListAsync(cancellationToken: ct);

            if (
                BuiltinPresetRenames.IdRedirects.TryGetValue(key: preset.Id, value: out Ulid replacementId)
                && builtinIds.Contains(item: replacementId)
            )
            {
                await RedirectLinksAsync(links: links, replacementId: replacementId, ct: ct);
                context.EncodingPresets.Remove(entity: preset);
                continue;
            }

            if (links.Count > 0)
            {
                // Someone chose this preset. Hand it to them rather than delete
                // their configuration out from under them.
                preset.IsBuiltIn = false;
                preset.Source = "retired-builtin";
                continue;
            }

            context.EncodingPresets.Remove(entity: preset);
        }

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    private async Task RedirectLinksAsync(
        List<EncodingPresetFolder> links,
        Ulid replacementId,
        CancellationToken ct
    )
    {
        foreach (EncodingPresetFolder link in links)
        {
            bool replacementAlreadyLinked = await context.EncodingPresetFolders.AnyAsync(
                predicate: existing =>
                    existing.PresetId == replacementId && existing.FolderId == link.FolderId,
                cancellationToken: ct
            );

            // The composite key is (PresetId, FolderId): re-pointing onto a row
            // that already exists would collide, so drop the duplicate instead.
            if (replacementAlreadyLinked)
            {
                context.EncodingPresetFolders.Remove(entity: link);
                continue;
            }

            context.EncodingPresetFolders.Remove(entity: link);
            context.EncodingPresetFolders.Add(
                entity: new()
                {
                    PresetId = replacementId,
                    FolderId = link.FolderId,
                    IsDefault = link.IsDefault,
                }
            );
        }
    }
}
