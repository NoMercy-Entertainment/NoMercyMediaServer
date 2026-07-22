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

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(LibraryId))]
[Index(propertyName: nameof(ImdbId))]
[Index(propertyName: nameof(ReleaseDate))]
[Index(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(TitleSort))]
[Index(propertyName: nameof(CreatedAt))]
[Index(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(CreatedAt))]
public class Movie : ColorPaletteTimeStamps, IHasLibrary
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "title_sort")]
    public string TitleSort { get; set; } = string.Empty;

    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    [JsonProperty(propertyName: "show")]
    public bool Show { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "adult")]
    public bool Adult { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "budget")]
    public int? Budget { get; set; }

    [JsonProperty(propertyName: "homepage")]
    public string? Homepage { get; set; }

    [JsonProperty(propertyName: "imdb_id")]
    public string? ImdbId { get; set; }

    [JsonProperty(propertyName: "original_title")]
    public string? OriginalTitle { get; set; }

    [JsonProperty(propertyName: "original_language")]
    public string? OriginalLanguage { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "popularity")]
    public double? Popularity { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonProperty(propertyName: "revenue")]
    public long? Revenue { get; set; }

    [JsonProperty(propertyName: "runtime")]
    public int? Runtime { get; set; }

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }

    [JsonProperty(propertyName: "tagline")]
    public string? Tagline { get; set; }

    [JsonProperty(propertyName: "trailer")]
    public string? Trailer { get; set; }

    [JsonProperty(propertyName: "video")]
    public string? Video { get; set; }

    [JsonProperty(propertyName: "vote_average")]
    public double? VoteAverage { get; set; }

    [JsonProperty(propertyName: "vote_count")]
    public int? VoteCount { get; set; }

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty(propertyName: "alternative_titles")]
    public ICollection<AlternativeTitle> AlternativeTitles { get; set; } = [];

    [JsonProperty(propertyName: "cast")]
    public ICollection<Cast> Cast { get; set; } = [];

    [JsonProperty(propertyName: "certifications")]
    public ICollection<CertificationMovie> CertificationMovies { get; set; } = [];

    [JsonProperty(propertyName: "crew")]
    public ICollection<Crew> Crew { get; set; } = [];

    [JsonProperty(propertyName: "genre")]
    public ICollection<GenreMovie> GenreMovies { get; set; } = [];

    [JsonProperty(propertyName: "keywords")]
    public ICollection<KeywordMovie> KeywordMovies { get; set; } = [];

    [JsonProperty(propertyName: "media")]
    public ICollection<Media.Media> Media { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "seasons")]
    public ICollection<Season> Seasons { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [JsonProperty(propertyName: "user_data")]
    public ICollection<UserData> UserData { get; set; } = [];

    [InverseProperty(property: "MovieFrom")]
    public ICollection<Recommendation> RecommendationFrom { get; set; } = [];

    [InverseProperty(property: "MovieTo")]
    public ICollection<Recommendation> RecommendationTo { get; set; } = [];

    [InverseProperty(property: "MovieFrom")]
    public ICollection<Similar> SimilarFrom { get; set; } = [];

    [InverseProperty(property: "MovieTo")]
    public ICollection<Similar> SimilarTo { get; set; } = [];

    [JsonProperty(propertyName: "movie_user")]
    public ICollection<MovieUser> MovieUser { get; set; } = [];

    [JsonProperty(propertyName: "video_files")]
    public ICollection<VideoFile> VideoFiles { get; set; } = [];

    [JsonProperty(propertyName: "playback_preferences")]
    public ICollection<PlaybackPreference> PlaybackPreferences { get; set; } = [];

    [JsonProperty(propertyName: "watch_providers")]
    public ICollection<WatchProviderMedia> WatchProviderMedia { get; set; } = [];

    [JsonProperty(propertyName: "companies")]
    public ICollection<CompanyMovie> CompaniesMovies { get; set; } = [];

    public string CreateFolderName()
    {
        return string.Concat(args: [Title.CleanFileName().Shorten(), ".(", ReleaseDate.ParseYear(), ")"])
            .CleanFileName();
    }

    public string CreateTitle()
    {
        return string.Concat(args: [Title, " (", ReleaseDate.ParseYear(), ") NoMercy"]);
    }

    public string CreateFileName()
    {
        return string.Concat(args: [Title.CleanFileName().Shorten(), ".(", ReleaseDate.ParseYear(), ").NoMercy"]
        );
    }
}
