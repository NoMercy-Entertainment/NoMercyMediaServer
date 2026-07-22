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

public class MetadataTracks : Timestamps
{
    [Column(name: "Video")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _video { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "video")]
    public List<IVideo>? Video
    {
        get => _video != null ? JsonConvert.DeserializeObject<List<IVideo>>(value: _video) : null;
        init => _video = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "Audio")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _audio { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "audio")]
    public List<IAudio>? Audio
    {
        get => _audio != null ? JsonConvert.DeserializeObject<List<IAudio>>(value: _audio) : null;
        init => _audio = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "Subtitles")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitles { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "subtitles")]
    public List<ISubtitle>? Subtitles
    {
        get =>
            _subtitles != null ? JsonConvert.DeserializeObject<List<ISubtitle>>(value: _subtitles) : null;
        init => _subtitles = JsonConvert.SerializeObject(value: value);
    }
}
