using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record LoloMoRowItemDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("media_type")] public string? MediaType { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("genres")] public GenreDto[]? LoloMos { get; set; }
    [JsonProperty("rating")] public RatingClass? Rating { get; set; }
    [JsonProperty("videos")] public VideoDto[]? Videos { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }


    public LoloMoRowItemDto(GenreMovie genreMovie)
    {
        Id = genreMovie.Movie.Id;
        Title = genreMovie.Movie.Title;
        Overview = genreMovie.Movie.Overview;
        Poster = genreMovie.Movie.Poster;
        Backdrop = genreMovie.Movie.Backdrop;
        TitleSort = genreMovie.Movie.Title.TitleSort(genreMovie.Movie.ReleaseDate);
        Year = genreMovie.Movie.ReleaseDate.ParseYear();
        MediaType = "movie";
        Link = new($"/movie/{Id}", UriKind.Relative);
        ColorPalette = genreMovie.Movie.ColorPalette;
    }

    public LoloMoRowItemDto(GenreTv genreTv)
    {
        Id = genreTv.Tv.Id;
        Title = genreTv.Tv.Title;
        Overview = genreTv.Tv.Overview;
        Poster = genreTv.Tv.Poster;
        Backdrop = genreTv.Tv.Backdrop;
        TitleSort = genreTv.Tv.Title.TitleSort(genreTv.Tv.FirstAirDate);
        Type = genreTv.Tv.Type;
        Year = genreTv.Tv.FirstAirDate.ParseYear();
        MediaType = "tv";
        Link = new($"/tv/{Id}", UriKind.Relative);
        ColorPalette = genreTv.Tv.ColorPalette;
    }
}
