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

public class MusicBrainzRecording
{
    [JsonProperty("disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty("video")]
    public bool Video { get; set; }

    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("length")]
    public int? Length { get; set; }

    [JsonProperty("genres")]
    public MusicBrainzGenreDetails[] Genres { get; set; } = [];

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;
}
