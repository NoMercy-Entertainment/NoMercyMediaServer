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

using NoMercy.Database.Models.Playlists;

namespace NoMercy.Api.DTOs.Playlists;

/// <summary>
/// Wire-format (lowercase, API-contract) mapping for <see cref="PlaylistItemKind"/>.
/// Kept separate from the enum's own name so the JSON contract ("movie", "tv",
/// "episode", "special") never silently changes just because a C# member gets
/// renamed, and so it never collides with the unrelated "type"
/// route-discriminator strings NmCardDto/CardData already use (which, for
/// specials, is the legacy plural "specials" — a different field for a
/// different purpose). Deliberately VIDEO-ONLY — there is no "track" kind;
/// music has its own separate playlist feature this DTO never touches.
/// </summary>
public static class PlaylistItemKindWire
{
    public static string ToWireString(this PlaylistItemKind kind) =>
        kind switch
        {
            PlaylistItemKind.Movie => "movie",
            PlaylistItemKind.Tv => "tv",
            PlaylistItemKind.Episode => "episode",
            PlaylistItemKind.Special => "special",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    public static bool TryParse(string? value, out PlaylistItemKind kind)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "movie":
                kind = PlaylistItemKind.Movie;
                return true;
            case "tv":
                kind = PlaylistItemKind.Tv;
                return true;
            case "episode":
                kind = PlaylistItemKind.Episode;
                return true;
            case "special":
                kind = PlaylistItemKind.Special;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
