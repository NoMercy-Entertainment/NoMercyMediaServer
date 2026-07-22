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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(VideoFileId), additionalPropertyNames: [nameof(UserId), nameof(MovieId)], IsUnique = true)]
[Index(propertyName: nameof(VideoFileId), additionalPropertyNames: [nameof(UserId), nameof(TvId)], IsUnique = true)]
[Index(propertyName: nameof(VideoFileId), additionalPropertyNames: [nameof(UserId), nameof(CollectionId)], IsUnique = true)]
[Index(propertyName: nameof(VideoFileId), additionalPropertyNames: [nameof(UserId), nameof(SpecialId)], IsUnique = true)]
[Index(propertyName: nameof(UserId))]
[Index(propertyName: nameof(MovieId))]
[Index(propertyName: nameof(TvId))]
[Index(propertyName: nameof(CollectionId))]
[Index(propertyName: nameof(SpecialId))]
[Index(propertyName: nameof(VideoFileId))]
[Index(propertyName: nameof(UserId), additionalPropertyNames: nameof(LastPlayedDate))]
public class UserData : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "rating")]
    public int? Rating { get; set; }

    [JsonProperty(propertyName: "last_played_date")]
    public string? LastPlayedDate { get; set; }

    [JsonProperty(propertyName: "audio")]
    public string? Audio { get; set; }

    [JsonProperty(propertyName: "subtitle")]
    public string? Subtitle { get; set; }

    [JsonProperty(propertyName: "subtitle_type")]
    public string? SubtitleType { get; set; }

    [JsonProperty(propertyName: "time")]
    public int? Time { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty(propertyName: "collection_id")]
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty(propertyName: "special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }

    [JsonProperty(propertyName: "video_file_id")]
    public Ulid VideoFileId { get; set; }
    public VideoFile VideoFile { get; set; } = null!;

    [JsonProperty(propertyName: "removed_from_continue_watching")]
    public bool RemovedFromContinueWatching { get; set; }
}
