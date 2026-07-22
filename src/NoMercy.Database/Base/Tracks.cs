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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Database;

public class VideoTracks : Timestamps
{
    [Column(name: "Track")]
    [StringLength(maximumLength: 1024)]
    [JsonProperty(propertyName: "tracks")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _tracks { get; set; } = string.Empty;

    [NotMapped]
    public VideoTrack[] Tracks
    {
        get =>
            (_tracks != string.Empty ? JsonConvert.DeserializeObject<VideoTrack[]>(value: _tracks) : [])
            ?? [];
        set => _tracks = JsonConvert.SerializeObject(value: value);
    }
}
