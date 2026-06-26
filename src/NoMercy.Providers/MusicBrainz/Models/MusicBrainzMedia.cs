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

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzMedia
{
    [JsonProperty("track-count")]
    public int TrackCount { get; set; }

    [JsonProperty("position")]
    public int Position { get; set; }

    [JsonProperty("format")]
    public string Format { get; set; } = string.Empty;

    [JsonProperty("format-id")]
    public Guid? FormatId { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("tracks")]
    public MusicBrainzTrack[] Tracks { get; set; } = [];
}
