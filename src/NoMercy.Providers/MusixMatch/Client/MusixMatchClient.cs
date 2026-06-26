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

using NoMercy.Providers.MusixMatch.Models;
using static System.String;

namespace NoMercy.Providers.MusixMatch.Client;

public class MusixmatchClient : MusixMatchBaseClient
{
    public Task<MusixMatchSubtitleGet?> SongSearch(
        MusixMatchTrackSearchParameters musixMatchTrackParameters,
        bool priority = false
    )
    {
        Dictionary<string, string?> additionalArguments = new()
        {
            ["q_artist"] = musixMatchTrackParameters.Artist,
            ["q_track"] = musixMatchTrackParameters.Title,
        };

        if (musixMatchTrackParameters.Album != null)
            additionalArguments.Add("q_album", musixMatchTrackParameters.Album);
        if (
            musixMatchTrackParameters.Artists != null
            && musixMatchTrackParameters.Artists.Length > 0
        )
            additionalArguments.Add(
                "q_artists",
                Join(",", musixMatchTrackParameters.Artists ?? [])
            );
        if (
            musixMatchTrackParameters.Duration != null
            && musixMatchTrackParameters.Duration.Length > 0
        )
            additionalArguments.Add("q_duration", musixMatchTrackParameters.Duration ?? Empty);

        return Get<MusixMatchSubtitleGet>("macro.subtitles.get", additionalArguments, priority);
    }
}
