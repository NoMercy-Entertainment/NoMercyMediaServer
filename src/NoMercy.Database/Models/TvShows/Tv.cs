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
using NoMercy.Database.Infrastructure;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(LibraryId))]
[Index(propertyName: nameof(ImdbId))]
[Index(propertyName: nameof(TvdbId))]
[Index(propertyName: nameof(FirstAirDate))]
[Index(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(TitleSort))]
[Index(propertyName: nameof(CreatedAt))]
[Index(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(CreatedAt))]
public class Tv : ColorPaletteTimeStamps, IHasLibrary
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "titleSort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "have_episodes")]
    public int? HaveEpisodes { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    [JsonProperty(propertyName: "first_air_date")]
    public DateTime? FirstAirDate { get; set; }

    [JsonProperty(propertyName: "homepage")]
    public string? Homepage { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "in_production")]
    public bool? InProduction { get; set; }

    [JsonProperty(propertyName: "last_episode_to_air")]
    public int? LastEpisodeToAir { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string? MediaType { get; set; }

    [JsonProperty(propertyName: "next_episode_to_air")]
    public int? NextEpisodeToAir { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int NumberOfEpisodes { get; set; }

    [JsonProperty(propertyName: "number_of_seasons")]
    public int? NumberOfSeasons { get; set; }

    [JsonProperty(propertyName: "origin_country")]
    public string? OriginCountry { get; set; }

    [JsonProperty(propertyName: "original_language")]
    public string? OriginalLanguage { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double? Popularity { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "spoken_languages")]
    public string? SpokenLanguages { get; set; }

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }

    [JsonProperty(propertyName: "tagline")]
    public string? Tagline { get; set; }

    [JsonProperty(propertyName: "trailer")]
    public string? Trailer { get; set; }

    [JsonProperty(propertyName: "tvdb_id")]
    public int? TvdbId { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double? VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int? VoteCount { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty(propertyName: "alternative_titles")]
    public ICollection<AlternativeTitle> AlternativeTitles { get; set; } = [];

    [JsonProperty(propertyName: "casts")]
    public ICollection<Cast> Cast { get; set; } = [];

    [JsonProperty(propertyName: "certifications")]
    public ICollection<CertificationTv> CertificationTvs { get; set; } = [];

    [JsonProperty(propertyName: "crews")]
    public ICollection<Crew> Crew { get; set; } = [];

    [JsonProperty(propertyName: "creators")]
    public ICollection<Creator> Creators { get; set; } = [];

    [JsonProperty(propertyName: "genres")]
    public ICollection<GenreTv> GenreTvs { get; set; } = [];

    [JsonProperty(propertyName: "keywords")]
    public ICollection<KeywordTv> KeywordTvs { get; set; } = [];

    [JsonProperty(propertyName: "medias")]
    public ICollection<Media.Media> Media { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "seasons")]
    public ICollection<Season> Seasons { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [JsonProperty(propertyName: "user_data")]
    public ICollection<UserData> UserData { get; set; } = [];

    [JsonProperty(propertyName: "episodes")]
    public ICollection<Episode> Episodes { get; set; } = [];

    [InverseProperty(property: "TvFrom")]
    public ICollection<Recommendation> RecommendationFrom { get; set; } = [];

    [InverseProperty(property: "TvTo")]
    public ICollection<Recommendation> RecommendationTo { get; set; } = [];

    [InverseProperty(property: "TvFrom")]
    public ICollection<Similar> SimilarFrom { get; set; } = [];

    [InverseProperty(property: "TvTo")]
    public ICollection<Similar> SimilarTo { get; set; } = [];

    [JsonProperty(propertyName: "tv_user")]
    public ICollection<TvUser> TvUser { get; set; } = [];

    [JsonProperty(propertyName: "playback_preferences")]
    public ICollection<PlaybackPreference> PlaybackPreferences { get; set; } = [];

    [JsonProperty(propertyName: "watch_providers")]
    public ICollection<WatchProviderMedia> WatchProviderMedia { get; set; } = [];

    [JsonProperty(propertyName: "networks")]
    public ICollection<NetworkTv> NetworkTvs { get; set; } = [];

    [JsonProperty(propertyName: "companies")]
    public ICollection<CompanyTv> CompaniesTvs { get; set; } = [];

    public string CreateFolderName()
    {
        return "/"
            + string.Concat(args: [Title.CleanFileName().Shorten(), ".(", FirstAirDate.ParseYear(), ")"])
                .CleanFileName();
    }
}
