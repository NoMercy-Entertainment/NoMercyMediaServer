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

using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Maps a <see cref="MediaItemRef"/> (already carrying TMDB identity fields
/// threaded through from the DB at job-build time) to the lean
/// <see cref="BlueprintIdentity"/> block. DB-free — the caller resolves the
/// DB row and passes the ref in.
/// </summary>
public static class BlueprintIdentityFactory
{
    public static BlueprintIdentity From(MediaItemRef media) =>
        media switch
        {
            MovieMediaRef movie => new(
                Type: "movie",
                TmdbId: movie.Id,
                Show: null,
                Season: null,
                Episode: null,
                Title: movie.Title,
                Year: movie.Year
            ),
            EpisodeMediaRef episode => new(
                Type: "episode",
                TmdbId: episode.Id,
                Show: new(TmdbId: episode.ShowTmdbId, Title: episode.ShowTitle),
                Season: episode.SeasonNumber,
                Episode: episode.EpisodeNumber,
                Title: episode.Title,
                Year: episode.Year
            ),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(media),
                actualValue: media.Type,
                message: $"No blueprint identity mapping for media type '{media.Type}'."
            ),
        };
}
