using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO.Components;

/// <summary>
/// Data for NMGenreCard component - genre category card.
/// </summary>
public record GenreCardData
{
    [JsonProperty("id")] public dynamic? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; } = string.Empty;
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;
    [JsonProperty("rating")] public RatingClass? Rating { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("content_ratings")] public IEnumerable<ContentRating> ContentRatings { get; set; } = [];
    [JsonProperty("have_items")] public int? HaveItems { get; set; }
    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }

    public GenreCardData()
    {
    }

    public GenreCardData(Genre genre)
    {
        Id = genre.Id;
        Title = genre.Name;
        TitleSort = genre.Name;
        Type = "genre";
        Link = new($"/genre/{genre.Id}", UriKind.Relative);
        NumberOfItems = genre.GenreMovies.Count + genre.GenreTvShows.Count;
        HaveItems = genre.GenreMovies.Count(gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
                    + genre.GenreTvShows.Count(gt => gt.Tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)));
    }

    public GenreCardData(MusicGenre musicGenre)
    {
        Id = musicGenre.Id;
        Title = musicGenre.Name.ToTitleCase();
        TitleSort = musicGenre.Name.TitleSort();
        Type = "genre";
        Link = new($"/music/genres/{musicGenre.Id}", UriKind.Relative);
        NumberOfItems = musicGenre.AlbumMusicGenres.Count + musicGenre.ArtistMusicGenres.Count;
        HaveItems = musicGenre.AlbumMusicGenres.Count(ga => ga.Album.AlbumTrack.Count != 0)
                    + musicGenre.ArtistMusicGenres.Count(ga => ga.Artist.ArtistTrack.Count != 0);
    }

    public GenreCardData(GenreWithCountsDto dto)
    {
        Id = dto.Id;
        Title = dto.Name.ToTitleCase();
        TitleSort = dto.Name.ToTitleCase();
        Type = "genre";
        Link = new($"/genre/{dto.Id}", UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }
}
