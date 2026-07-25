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
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;

namespace NoMercy.MediaProcessing.Libraries;

/// <summary>
/// Gives a freshly-created library folder automatic multi-resolution HLS
/// encoding with zero manual profile picking. Attaches a single default
/// <see cref="EncodingPresetFolder"/> link — the same table
/// <c>VideoEncodeJob</c> resolves its presets from and
/// <c>AutoEncodeSubscriber</c> gates its dispatch decision on — so a brand
/// new folder starts encoding the moment media lands in it.
///
/// Idempotent per folder: a folder that already carries any preset link
/// (default or user-picked) is left untouched. Callers are responsible for
/// invoking this only on genuinely new folders — re-adding an existing
/// folder to another library, and every folder that already existed before
/// this feature shipped, must never gain a link they didn't already have.
/// </summary>
public class DefaultEncodingPresetLinker(
    MediaContext context,
    ILogger<DefaultEncodingPresetLinker> logger
) : IDefaultEncodingPresetLinker
{
    /// <summary>
    /// Read by name (not by the builtin's computed Ulid) so a rename degrades
    /// to the IsDefault fallback instead of throwing. Bound to the constant
    /// rather than a literal: a renamed builtin would otherwise resolve to
    /// nothing here and silently leave every new folder without auto-encode.
    /// </summary>
    private const string DefaultBuiltinPresetName = BuiltinPresets.DefaultStreamingPresetName;

    public async Task<bool> AttachDefaultIfMissingAsync(
        Ulid folderId,
        CancellationToken ct = default
    )
    {
        bool alreadyLinked = await context
            .EncodingPresetFolders.AsNoTracking()
            .AnyAsync(link => link.FolderId == folderId, ct);

        if (alreadyLinked)
            return false;

        Ulid? presetId = await ResolveDefaultPresetIdAsync(ct);
        if (presetId is null)
        {
            logger.LogWarning(
                "DefaultEncodingPresetLinker: no default preset available (builtin '{Name}' missing, no existing IsDefault link to fall back to) — folder {FolderId} left without auto-encode", [DefaultBuiltinPresetName, folderId]
            );
            return false;
        }

        context.EncodingPresetFolders.Add(
            new()
            {
                FolderId = folderId,
                PresetId = presetId.Value,
                IsDefault = true,
            }
        );

        await context.SaveChangesAsync(ct);
        return true;
    }

    private async Task<Ulid?> ResolveDefaultPresetIdAsync(CancellationToken ct)
    {
        EncodingPreset? standard = await context
            .EncodingPresets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == DefaultBuiltinPresetName && p.IsBuiltIn, ct);

        if (standard is not null)
            return standard.Id;

        // Fallback: reuse whatever preset is already flagged as the default
        // on some other folder's link, if one exists.
        List<Ulid> fallback = await context
            .EncodingPresetFolders.AsNoTracking()
            .Where(link => link.IsDefault)
            .Select(link => link.PresetId)
            .Distinct()
            .Take(1)
            .ToListAsync(ct);

        return fallback.Count > 0 ? fallback[0] : null;
    }
}
