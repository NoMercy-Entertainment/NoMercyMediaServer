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

[PrimaryKey(nameof(Id))]
[Index(nameof(VideoFileId), [nameof(UserId), nameof(MovieId)], IsUnique = true)]
[Index(nameof(VideoFileId), [nameof(UserId), nameof(TvId)], IsUnique = true)]
[Index(nameof(VideoFileId), [nameof(UserId), nameof(CollectionId)], IsUnique = true)]
[Index(nameof(VideoFileId), [nameof(UserId), nameof(SpecialId)], IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(MovieId))]
[Index(nameof(TvId))]
[Index(nameof(CollectionId))]
[Index(nameof(SpecialId))]
[Index(nameof(VideoFileId))]
[Index(nameof(UserId), nameof(LastPlayedDate))]
public class UserData : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty("rating")]
    public int? Rating { get; set; }

    [JsonProperty("last_played_date")]
    public string? LastPlayedDate { get; set; }

    [JsonProperty("audio")]
    public string? Audio { get; set; }

    [JsonProperty("subtitle")]
    public string? Subtitle { get; set; }

    [JsonProperty("subtitle_type")]
    public string? SubtitleType { get; set; }

    [JsonProperty("time")]
    public int? Time { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty("movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty("tv_id")]
    public int? TvId { get; set; }
    public Tv? Tv { get; set; }

    [JsonProperty("collection_id")]
    public int? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    [JsonProperty("special_id")]
    public Ulid? SpecialId { get; set; }
    public Special? Special { get; set; }

    [JsonProperty("video_file_id")]
    public Ulid VideoFileId { get; set; }
    public VideoFile VideoFile { get; set; } = null!;

    [JsonProperty("removed_from_continue_watching")]
    public bool RemovedFromContinueWatching { get; set; }
}
