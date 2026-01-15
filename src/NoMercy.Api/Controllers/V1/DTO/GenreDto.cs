using Newtonsoft.Json;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.Controllers.V1.DTO;

public record GenreDto
{
    [JsonProperty("id")] public dynamic Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }

    public Uri Link { get; set; }

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