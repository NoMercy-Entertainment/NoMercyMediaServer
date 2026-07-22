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
            [key: "q_artist"] = musixMatchTrackParameters.Artist,
            [key: "q_track"] = musixMatchTrackParameters.Title,
        };

        if (musixMatchTrackParameters.Album != null)
            additionalArguments.Add(key: "q_album", value: musixMatchTrackParameters.Album);
        if (
            musixMatchTrackParameters.Artists is { Length: > 0 }
        )
            additionalArguments.Add(
                key: "q_artists",
                value: Join(separator: ",", value: musixMatchTrackParameters.Artists ?? [])
            );
        if (
            musixMatchTrackParameters.Duration is { Length: > 0 }
        )
            additionalArguments.Add(key: "q_duration", value: musixMatchTrackParameters.Duration ?? Empty);

        return Get<MusixMatchSubtitleGet>(url: "macro.subtitles.get", query: additionalArguments, priority: priority);
    }
}
