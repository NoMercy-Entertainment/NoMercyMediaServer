using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Media;

public record TopMusicDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("type")] public string Type { get; set; } = "albums";
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    public TopMusicDto()
    {
        //
    }

    public TopMusicDto(PlaylistTrack musicPlay)
    {
        Id = musicPlay.Playlist.Id.ToString();
        Name = musicPlay.Playlist.Name;
        ColorPalette = musicPlay.Playlist.ColorPalette;
        Type = "playlist";
        Link = new($"/music/playlists/{Id}", UriKind.Relative);
        Cover = musicPlay.Playlist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
    }

    public TopMusicDto(AlbumTrack albumTrack)
    {
        Id = albumTrack.Album.Id.ToString();
        Name = albumTrack.Album.Name;
        ColorPalette = albumTrack.Album.ColorPalette;
        Type = "album";
        Link = new($"/music/album/{Id}", UriKind.Relative);
        Cover = albumTrack.Album.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
    }

    public TopMusicDto(ArtistTrack artistTrack)
    {
        Id = artistTrack.Artist.Id.ToString();
        Name = artistTrack.Artist.Name;
        ColorPalette = artistTrack.Artist.ColorPalette;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);
        Cover = artistTrack.Artist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
    }
}