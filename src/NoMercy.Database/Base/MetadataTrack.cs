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

public class MetadataTrack : Timestamps
{
    [Column(name: "Video")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _video { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "video", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IVideo? Video
    {
        get => _video != null ? JsonConvert.DeserializeObject<IVideo>(value: _video) : null;
        init =>
            _video =
                value != null
                    ? JsonConvert.SerializeObject(
                        value: value,
                        formatting: Formatting.None,
                        settings: new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                    )
                    : null;
    }

    [Column(name: "Audio")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _audio { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "audio", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IAudio? Audio
    {
        get => _audio != null ? JsonConvert.DeserializeObject<IAudio>(value: _audio) : null;
        init =>
            _audio =
                value != null
                    ? JsonConvert.SerializeObject(
                        value: value,
                        formatting: Formatting.None,
                        settings: new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                    )
                    : null;
    }

    [Column(name: "Subtitles")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitle { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "subtitle", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ISubtitle? Subtitle
    {
        get => _subtitle != null ? JsonConvert.DeserializeObject<ISubtitle>(value: _subtitle) : null;
        init =>
            _subtitle =
                value != null
                    ? JsonConvert.SerializeObject(
                        value: value,
                        formatting: Formatting.None,
                        settings: new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                    )
                    : null;
    }
}
