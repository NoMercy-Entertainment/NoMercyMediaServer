using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Common;

public record GenreDto
{
    [JsonProperty("id")] public dynamic Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }

    public Uri Link { get; set; }

    public GenreDto()
    {
        Id = 0;
        Name = string.Empty;
        Link = new("/genre/0", UriKind.Relative);
    }

    public GenreDto(GenreMovie genreMovie)
    {
        Id = genreMovie.GenreId;
        Name = genreMovie.Genre.Name;
        Link = new($"/genre/{Id}", UriKind.Relative);
    }

    public GenreDto(GenreTv genreTv)
    {
        Id = genreTv.GenreId;
        Name = genreTv.Genre.Name;
        Link = new($"/genre/{Id}", UriKind.Relative);
    }

    public GenreDto(Genre genreMovie)
    {
        Id = genreMovie.Id;
        Name = genreMovie.Name;
        Link = new($"/genre/{Id}", UriKind.Relative);
    }

    public GenreDto(TmdbGenre tmdbGenreMovie)
    {
        Id = tmdbGenreMovie.Id;
        Name = tmdbGenreMovie.Name ?? string.Empty;
        Link = new($"/genre/{Id}", UriKind.Relative);
    }

    public GenreDto(ArtistMusicGenre artistMusicGenre)
    {
        Id = artistMusicGenre.MusicGenreId;
        Name = artistMusicGenre.MusicGenre.Name;
        Link = new($"/music/genres/{Id}", UriKind.Relative);
    }

    public GenreDto(AlbumMusicGenre artistMusicGenre)
    {
        Id = artistMusicGenre.MusicGenreId;
        Name = artistMusicGenre.MusicGenre.Name;
        Link = new($"/music/genres/{Id}", UriKind.Relative);
    }
}