using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbShowOrMovie : TmdbBase
{
    [JsonProperty("adult")] public bool? Adult { get; set; }
    [JsonProperty("genres")] public int[]? GenresIds { get; set; } = [];
    [JsonProperty("original_title")] public string? OriginalTitle { get; set; }
    [JsonProperty("tagline")] public string? Tagline { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
    [JsonProperty("video")] public bool? Video { get; set; }

    [JsonProperty("first_air_date")] public DateTime? FirstAirDate { get; set; }
    [JsonProperty("genre_ids")] public int?[] GenreIds { get; set; } = [];
    [JsonProperty("name")] public string? Name { get; set; } = string.Empty;
    [JsonProperty("origin_country")] public string?[] OriginCountry { get; set; } = [];
    [JsonProperty("original_name")] public string? OriginalName { get; set; } = string.Empty;
    [JsonProperty("type")] public string? MediaType { get; set; } = string.Empty;

    public TmdbShowOrMovie(TmdbMovie movie)
    {
        Id = movie.Id;
        OriginalLanguage = movie.OriginalLanguage;
        Overview = movie.Overview;
        Popularity = movie.Popularity;
        PosterPath = movie.PosterPath;
        VoteAverage = movie.VoteAverage;
        VoteCount = movie.VoteCount;
        Adult = movie.Adult;
        GenresIds = movie.GenresIds;
        OriginalTitle = movie.OriginalTitle;
        Tagline = movie.Tagline;
        Title = movie.Title;
        ReleaseDate = movie.ReleaseDate;
        Video = movie.Video;
    }

    public TmdbShowOrMovie(TmdbTvShow show)
    {
        Id = show.Id;
        OriginalLanguage = show.OriginalLanguage;
        Overview = show.Overview;
        Popularity = show.Popularity;
        PosterPath = show.PosterPath;
        VoteAverage = show.VoteAverage;
        VoteCount = show.VoteCount;
        FirstAirDate = show.FirstAirDate;
        GenreIds = show.GenreIds;
        Name = show.Name;
        OriginCountry = show.OriginCountry;
        OriginalName = show.OriginalName;
        MediaType = show.MediaType;
    }
}