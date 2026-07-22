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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(TvId))]
[Index(propertyName: nameof(SeasonId))]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(EpisodeNumber))]
[Index(propertyName: nameof(SeasonNumber))]
[Index(propertyName: nameof(AirDate))]
[Index(propertyName: nameof(ImdbId))]
[Index(propertyName: nameof(TvdbId))]
[Index(propertyName: nameof(TvId), additionalPropertyNames: nameof(SeasonNumber))]
[Index(propertyName: nameof(TvId), additionalPropertyNames: [nameof(SeasonNumber), nameof(EpisodeNumber)])]
public class Episode : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "air_date")]
    public DateTime? AirDate { get; set; }

    [JsonProperty(propertyName: "episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "production_code")]
    public string? ProductionCode { get; set; }

    [JsonProperty(propertyName: "season_number")]
    public int SeasonNumber { get; set; }

    [JsonProperty(propertyName: "still")]
    public string? Still { get; set; }

    [JsonProperty(propertyName: "tvdb_id")]
    public int? TvdbId { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public float? VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int? VoteCount { get; set; }

    [JsonProperty(propertyName: "tv_id")]
    public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    [JsonProperty(propertyName: "season_id")]
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    [JsonProperty(propertyName: "casts")]
    public ICollection<Cast> Cast { get; set; } = [];

    [JsonProperty(propertyName: "crews")]
    public ICollection<Crew> Crew { get; set; } = [];

    [JsonProperty(propertyName: "special_items")]
    public ICollection<SpecialItem> SpecialItems { get; set; } = [];

    [JsonProperty(propertyName: "video_files")]
    public ICollection<VideoFile> VideoFiles { get; set; } = [];

    [JsonProperty(propertyName: "medias")]
    public ICollection<Media.Media> Media { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "guest_stars")]
    public ICollection<GuestStar> GuestStars { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    public string CreateFolderName()
    {
        return "/"
            + string.Concat(values: [Tv.Title.CleanFileName().Shorten(), ".S", SeasonNumber.ToString(format: "00"), "E", EpisodeNumber.ToString(format: "00")]
                )
                .CleanFileName();
    }

    public string CreateTitle()
    {
        return string.Concat(values: [Tv.Title, " S", SeasonNumber.ToString(format: "00"), "E", EpisodeNumber.ToString(format: "00"), " ", Title, " NoMercy"]
        );
    }

    public string CreateFileName()
    {
        return string.Concat(values: [Tv.Title.CleanFileName().Shorten(), ".S", SeasonNumber.ToString(format: "00"), "E", EpisodeNumber.ToString(format: "00"), ".", Title.CleanFileName().Shorten(), ".NoMercy"]
            )
            .CleanFileName();
    }
}
