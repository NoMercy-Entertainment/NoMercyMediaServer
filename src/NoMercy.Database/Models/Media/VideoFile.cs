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
using NoMercy.Database.Infrastructure;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Filename), additionalPropertyNames: nameof(HostFolder), IsUnique = true)]
[Index(propertyName: nameof(EpisodeId))]
[Index(propertyName: nameof(MovieId))]
[Index(propertyName: nameof(Folder))]
[Index(propertyName: nameof(Quality))]
[Index(propertyName: nameof(Duration))]
[Index(propertyName: nameof(MovieId), additionalPropertyNames: nameof(Folder))]
[Index(propertyName: nameof(EpisodeId), additionalPropertyNames: nameof(Folder))]
public class VideoFile : VideoTracks
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "duration")]
    public string? Duration { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string Filename
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "host_folder")]
    public string HostFolder
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "languages")]
    public string Languages { get; set; } = string.Empty;

    [JsonProperty(propertyName: "quality")]
    public string Quality { get; set; } = string.Empty;

    [JsonProperty(propertyName: "share")]
    public string Share { get; set; } = string.Empty;

    [JsonProperty(propertyName: "subtitles")]
    public string? Subtitles { get; set; }

    [JsonProperty(propertyName: "chapters")]
    public string? Chapters { get; set; }

    [JsonProperty(propertyName: "episode_id")]
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }

    [JsonProperty(propertyName: "last_episode_number")]
    public int? LastEpisodeNumber { get; set; }

    [JsonProperty(propertyName: "movie_id")]
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }

    [JsonProperty(propertyName: "metadata_id")]
    public Ulid? MetadataId { get; set; }
    public Metadata? Metadata { get; set; }

    [JsonProperty(propertyName: "user_data")]
    public ICollection<UserData> UserData { get; set; } = [];
}
