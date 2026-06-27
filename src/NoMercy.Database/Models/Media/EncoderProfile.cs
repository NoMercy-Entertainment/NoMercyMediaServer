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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(nameof(Id))]
public class EncoderProfile : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [Key]
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("container")]
    public string? Container { get; set; }

    [JsonProperty("type")]
    public string? Param { get; set; }

    [Column("VideoProfile")]
    [JsonProperty("video_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _videoProfiles { get; set; } = string.Empty;

    [NotMapped]
    public VideoProfile[] VideoProfiles
    {
        get =>
            _videoProfiles != string.Empty
                ? JsonConvert.DeserializeObject<VideoProfile[]>(_videoProfiles)!
                : [];
        set => _videoProfiles = JsonConvert.SerializeObject(value);
    }

    [Column("AudioProfile")]
    [JsonProperty("audio_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _audioProfiles { get; set; } = string.Empty;

    [NotMapped]
    public AudioProfile[] AudioProfiles
    {
        get =>
            _audioProfiles != string.Empty
                ? JsonConvert.DeserializeObject<AudioProfile[]>(_audioProfiles)!
                : [];
        set => _audioProfiles = JsonConvert.SerializeObject(value);
    }

    [Column("SubtitleProfile")]
    [JsonProperty("subtitle_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _subtitleProfiles { get; set; } = string.Empty;

    [NotMapped]
    public SubtitleProfile[] SubtitleProfiles
    {
        get =>
            _subtitleProfiles != string.Empty
                ? JsonConvert.DeserializeObject<SubtitleProfile[]>(_subtitleProfiles)!
                : [];
        set => _subtitleProfiles = JsonConvert.SerializeObject(value);
    }

    [Column("ThumbnailProfile")]
    [JsonProperty("thumbnail_profile")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string _thumbnailProfile { get; set; } = string.Empty;

    [NotMapped]
    public ThumbnailProfile? Thumbnails
    {
        get =>
            _thumbnailProfile != string.Empty
                ? JsonConvert.DeserializeObject<ThumbnailProfile>(_thumbnailProfile)
                : null;
        set =>
            _thumbnailProfile = value is not null
                ? JsonConvert.SerializeObject(value)
                : string.Empty;
    }

    [JsonProperty("encoder_profile_folder")]
    public ICollection<EncoderProfileFolder> EncoderProfileFolder { get; set; } = [];
}
