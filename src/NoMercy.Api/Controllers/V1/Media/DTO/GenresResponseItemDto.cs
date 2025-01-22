using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record GenresResponseItemDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("genres")] public GenreDto[]? Genres { get; set; }
    [JsonProperty("videoId")] public string? VideoId { get; set; }
    [JsonProperty("videos")] public VideoDto[] Videos { get; set; }

    public GenresResponseItemDto(GenreMovie movie)
    {
        Id = movie.Movie.Id;
        Backdrop = movie.Movie.Backdrop;
        Logo = movie.Movie.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;
        Year = movie.Movie.ReleaseDate.ParseYear();
        Overview = movie.Movie.Overview;
        ColorPalette = movie.Movie.ColorPalette;
        Poster = movie.Movie.Poster;
        Title = movie.Movie.Title;
        TitleSort = movie.Movie.Title
            .TitleSort(movie.Movie.ReleaseDate);
        HaveItems = movie.Movie.VideoFiles
            .Count(videoFile => videoFile.Folder != null);
        NumberOfItems = movie.Movie.VideoFiles.Count;
        Type = "movie";
        MediaType = "movie";
        Link = new($"/movie/{Id}", UriKind.Relative);
        Genres = movie.Movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie))
            .ToArray();
        VideoId = movie.Movie.Video;
        Videos = movie.Movie.Media
            .Where(media => media.Site == "YouTube")
            .Select(media => new VideoDto(media))
            .ToArray();
    }

    public GenresResponseItemDto(GenreTv tv)
    {
        Id = tv.Tv.Id;
        Backdrop = tv.Tv.Backdrop;
        Logo = tv.Tv.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;
        Year = tv.Tv.FirstAirDate.ParseYear();
        Overview = tv.Tv.Overview;
        ColorPalette = tv.Tv.ColorPalette;
        Poster = tv.Tv.Poster;
        Title = tv.Tv.Title;
        TitleSort = tv.Tv.Title.TitleSort(tv.Tv.FirstAirDate);

        Type = "tv";
        MediaType = "tv";
        Link = new($"/tv/{Id}", UriKind.Relative);
        NumberOfItems = tv.Tv.NumberOfEpisodes;
        HaveItems = tv.Tv.Episodes
            .Count(episode => episode.VideoFiles.Any(videoFile => videoFile.Folder != null));

        Genres = tv.Tv.GenreTvs
            .Select(genreTv => new GenreDto(genreTv))
            .ToArray();
        VideoId = tv.Tv.Trailer;
        Videos = tv.Tv.Media
            .Where(media => media.Site == "YouTube")
            .Select(media => new VideoDto(media))
            .ToArray();
    }

    public GenresResponseItemDto(CollectionMovie movie)
    {
        Id = movie.Movie.Id;
        Backdrop = movie.Movie.Backdrop;
        Logo = movie.Movie.Images
            .FirstOrDefault(media => media.Type == "logo")
            ?.FilePath;
        MediaType = "movie";
        Year = movie.Movie.ReleaseDate.ParseYear();
        Overview = movie.Movie.Overview;
        ColorPalette = movie.Movie.ColorPalette;
        Poster = movie.Movie.Poster;
        Title = movie.Movie.Title;
        TitleSort = movie.Movie.Title
            .TitleSort(movie.Movie.ReleaseDate);
        Type = "movie";
        Link = new($"/movie/{Id}", UriKind.Relative);
        HaveItems = movie.Movie.VideoFiles
            .Count(videoFile => videoFile.Folder != null);
        NumberOfItems = movie.Movie.VideoFiles.Count;
        Genres = movie.Movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie))
            .ToArray();
        VideoId = movie.Movie.Video;
        Videos = movie.Movie.Media
            .Where(media => media.Site == "YouTube")
            .Select(media => new VideoDto(media))
            .ToArray();
    }

    public GenresResponseItemDto(Collection collection)
    {
        string title = collection.Translations
            .FirstOrDefault()?.Title ?? collection.Title;

        string overview = collection.Translations
            .FirstOrDefault()?.Overview ?? collection.Overview ?? string.Empty;

        Id = collection.Id;
        Title = title;
        Overview = overview;
        Backdrop = collection.Backdrop;
        Logo = collection.Images
            .FirstOrDefault(media => media.Type == "logo")?.FilePath;
        Link = new($"/collection/{Id}", UriKind.Relative);
        Year = collection.CollectionMovies
            .MinBy(collectionMovie => collectionMovie.Movie.ReleaseDate)
            ?.Movie.ReleaseDate.ParseYear();

        ColorPalette = collection.ColorPalette;
        Poster = collection.Poster;
        TitleSort = collection.Title
            .TitleSort(collection.CollectionMovies
                .MinBy(collectionMovie => collectionMovie.Movie.ReleaseDate)
                ?.Movie.ReleaseDate.ParseYear());

        MediaType = "collection";
        Type = "collection";
        Link = new($"/collection/{Id}", UriKind.Relative);
        NumberOfItems = collection.Parts;
        HaveItems = collection.CollectionMovies
            .Count(collectionMovie => collectionMovie.Movie.VideoFiles.Any(v => v.Folder != null));
        Genres = collection.CollectionMovies
            .Select(genreTv => genreTv.Movie)
            .SelectMany(movie => movie.GenreMovies
                .Select(genreMovie => genreMovie.Genre))
            .Select(genre => new GenreDto(genre))
            .ToArray();

        VideoId = collection.CollectionMovies
            .FirstOrDefault()
            ?.Movie.Video;

        Videos = [];
    }


    public GenresResponseItemDto(Genre genre)
    {
        Id = genre.Id;
        Title = genre.Name;
        TitleSort = genre.Name;

        MediaType = "genres";
        Type = "genres";
        Link = new($"/genres/{Id}", UriKind.Relative);
        NumberOfItems = genre.GenreMovies.Count + genre.GenreTvShows.Count;
        HaveItems = genre.GenreMovies.Count(genreMovie => genreMovie.Movie.VideoFiles
                .Any(v => v.Folder != null))
            + genre.GenreTvShows.Count(genreTv => genreTv.Tv.Episodes
                .Any(episode => episode.VideoFiles.Any(v => v.Folder != null)));

        Videos = [];
    }
}
