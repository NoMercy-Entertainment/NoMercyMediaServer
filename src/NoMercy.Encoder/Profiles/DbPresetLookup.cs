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
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// <see cref="IPresetLookup"/> implementation that resolves V2 EncodingPreset
/// rows directly against <see cref="MediaContext"/>. Used by both the V1
/// resolver in the API layer and the V1 encode-job bridge so they walk the
/// parent chain through one shared adapter. Synchronous because
/// <see cref="PresetResolver"/> is a pure static method.
/// </summary>
public sealed class DbPresetLookup(MediaContext context) : IPresetLookup
{
    public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId)
    {
        EncodingPreset? row = context
            .EncodingPresets.AsNoTracking()
            .FirstOrDefault(p => p.Id == presetId);
        return row is null ? null : (row.ProfileJson, row.ParentPresetId);
    }
}
