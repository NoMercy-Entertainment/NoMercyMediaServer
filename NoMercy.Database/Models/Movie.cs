﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Helpers;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Movie : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("title_sort")] public string TitleSort { get; set; }
    [JsonProperty("duration")] public int? Duration { get; set; }
    [JsonProperty("show")] public bool Show { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("budget")] public int? Budget { get; set; }
    [JsonProperty("homepage")] public string? Homepage { get; set; }
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("original_title")] public string? OriginalTitle { get; set; }
    [JsonProperty("original_language")] public string? OriginalLanguage { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("popularity")] public double? Popularity { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
    [JsonProperty("revenue")] public long? Revenue { get; set; }
    [JsonProperty("runtime")] public int? Runtime { get; set; }
    [JsonProperty("status")] public string? Status { get; set; }
    [JsonProperty("tagline")] public string? Tagline { get; set; }
    [JsonProperty("trailer")] public string? Trailer { get; set; }
    [JsonProperty("video")] public string? Video { get; set; }
    [JsonProperty("vote_average")] public double? VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int? VoteCount { get; set; }

    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; }

    [JsonProperty("alternative_titles")] public ICollection<AlternativeTitle> AlternativeTitles { get; set; }

    [JsonProperty("cast")] public ICollection<Cast> Cast { get; set; } = new HashSet<Cast>();

    [JsonProperty("certifications")]
    public ICollection<CertificationMovie> CertificationMovies { get; set; } =
        new HashSet<CertificationMovie>();

    [JsonProperty("crew")] public ICollection<Crew> Crew { get; set; } = new HashSet<Crew>();

    [JsonProperty("genre")] public ICollection<GenreMovie> GenreMovies { get; set; }

    [JsonProperty("keywords")] public ICollection<KeywordMovie> KeywordMovies { get; set; }

    [JsonProperty("media")] public ICollection<Media> Media { get; set; }

    [JsonProperty("images")] public ICollection<Image> Images { get; set; }

    [JsonProperty("seasons")] public ICollection<Season> Seasons { get; set; }

    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; }

    [JsonProperty("user_data")] public ICollection<UserData> UserData { get; set; }

    [InverseProperty("MovieFrom")] public ICollection<Recommendation> RecommendationFrom { get; set; }

    [InverseProperty("MovieTo")] public ICollection<Recommendation> RecommendationTo { get; set; }

    [InverseProperty("MovieFrom")] public ICollection<Similar> SimilarFrom { get; set; }

    [InverseProperty("MovieTo")] public ICollection<Similar> SimilarTo { get; set; }

    [JsonProperty("movie_user")] public ICollection<MovieUser> MovieUser { get; set; }

    [JsonProperty("video_files")]
    public ICollection<VideoFile> VideoFiles { get; set; } = new HashSet<VideoFile>();

    public Movie(TmdbMovieAppends tmdbMovie, Ulid libraryId, string folder)
    {
        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        TitleSort = tmdbMovie.Title.TitleSort(tmdbMovie.ReleaseDate);
        Duration = tmdbMovie.Runtime;
        Folder = folder;
        Adult = tmdbMovie.Adult;
        Backdrop = tmdbMovie.BackdropPath;
        Budget = tmdbMovie.Budget;
        Homepage = tmdbMovie.Homepage?.ToString();
        ImdbId = tmdbMovie.ImdbId;
        OriginalTitle = tmdbMovie.OriginalTitle;
        OriginalLanguage = tmdbMovie.OriginalLanguage;
        Overview = tmdbMovie.Overview;
        Popularity = tmdbMovie.Popularity;
        Poster = tmdbMovie.PosterPath;
        ReleaseDate = tmdbMovie.ReleaseDate;
        Revenue = tmdbMovie.Revenue;
        Runtime = tmdbMovie.Runtime;
        Status = tmdbMovie.Status;
        Tagline = tmdbMovie.Tagline;
        Trailer = tmdbMovie.Video;
        Video = tmdbMovie.Video;
        VoteAverage = tmdbMovie.VoteAverage;
        VoteCount = tmdbMovie.VoteCount;

        LibraryId = libraryId;
    }

    public Movie()
    {
    }

    public Movie(Providers.TMDB.Models.Movies.TmdbMovie input, Ulid libraryId)
    {
        Id = input.Id;
        Title = input.Title;
        TitleSort = input.Title.TitleSort(input.ReleaseDate);
        Adult = input.Adult;
        Backdrop = input.BackdropPath;
        OriginalTitle = input.OriginalTitle;
        OriginalLanguage = input.OriginalLanguage;
        Overview = input.Overview;
        Popularity = input.Popularity;
        Poster = input.PosterPath;
        ReleaseDate = input.ReleaseDate;
        Tagline = input.Tagline;
        VoteAverage = input.VoteAverage;
        VoteCount = input.VoteCount;

        LibraryId = libraryId;
    }
}