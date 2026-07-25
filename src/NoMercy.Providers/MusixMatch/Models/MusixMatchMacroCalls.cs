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

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchMacroCalls
{
    [JsonProperty("track.lyrics.get")]
    public MusixMatchTrackLyricsGet? TrackLyricsGet { get; set; }

    [JsonProperty("track.snippet.get")]
    public MusixMatchTrackSnippetGet? TrackSnippetGet { get; set; }

    [JsonProperty("track.subtitles.get")]
    public MusixMatchTrackSubtitlesGet? TrackSubtitlesGet { get; set; }

    [JsonProperty("userblob.get")]
    public MusixMatchUserBlobGet? UserBlobGet { get; set; }

    [JsonProperty("matcher.track.get")]
    public MusixMatchMatcherTrackGet? MatcherTrackGet { get; set; }
}
