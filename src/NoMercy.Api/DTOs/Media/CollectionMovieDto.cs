using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Movies;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Media;

public record CollectionMovieDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public long Year { get; set; }
    [JsonProperty("genres")] public GenreDto[] Genres { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("rating")] public Certification? Rating { get; set; }

    [JsonProperty("videoId")] public string? VideoId { get; set; }

    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }

    public CollectionMovieDto(Movie movie)
    {
        string? title = movie.Translations.FirstOrDefault()?.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : movie.Title;
        
        TitleSort = movie.TitleSort;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : movie.Overview;

        Backdrop = movie.Backdrop;
        Favorite = movie.MovieUser.Count != 0;
        // Watched = movie.Watched;
        Logo = movie.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;

        MediaType = Config.MovieMediaType;
        ColorPalette = movie.ColorPalette;
        Poster = movie.Poster;
        Type = Config.MovieMediaType;
        Year = movie.ReleaseDate.ParseYear();
        Link = new($"/movie/{Id}", UriKind.Relative);
        Genres = movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie.Genre))
            .ToArray();

        Rating = movie.CertificationMovies
            .Select(certificationMovie => certificationMovie.Certification)
            .FirstOrDefault();

        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count > 0 ? 1 : 0;

        VideoId = movie.Video;
    }

    public CollectionMovieDto(Providers.TMDB.Models.Movies.TmdbMovie tmdbMovie)
    {
        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        Overview = tmdbMovie.Overview;
        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        TitleSort = tmdbMovie.TitleSort(tmdbMovie.ReleaseDate);
        Overview = tmdbMovie.Overview;
        Backdrop = tmdbMovie.BackdropPath;
        Favorite = false;
        Watched = false;
        // Logo = movie.Logo;
        Genres = [];
        Link = new($"/movie/{Id}", UriKind.Relative);
        MediaType = Config.MovieMediaType;
        ColorPalette = new();
        Poster = tmdbMovie.PosterPath;
        Type = Config.MovieMediaType;
        Year = tmdbMovie.ReleaseDate.ParseYear();

        NumberOfItems = 1;
        HaveItems = 0;

        VideoId = tmdbMovie.Video?.ToString();
    }
}