using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.Repositories;

public record CarouselResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("track_id")] public string? TrackId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("tracks")] public int Tracks { get; set; }

    public CarouselResponseItemDto(Artist artist)
    {
        ColorPalette = artist.ColorPalette;
        Cover = artist.Cover is not null 
            ? new Uri($"/images/music{artist.Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Folder = artist.Folder ?? "";
        Id = artist.Id.ToString();
        LibraryId = artist.LibraryId;
        Name = artist.Name;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = artist.ArtistTrack
            .DistinctBy(artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover is not null 
            ? new Uri($"/images/music{album.Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = album.Disambiguation;
        Description = album.Description;
        Folder = album.Folder ?? "";
        Id = album.Id.ToString();
        LibraryId = album.LibraryId;
        Name = album.Name;
        Type = "album";
        Link = new($"/music/album/{Id}", UriKind.Relative);

        Tracks = album.AlbumTrack
            .DistinctBy(albumTrack => albumTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(ArtistUser artistUser)
    {
        ColorPalette = artistUser.Artist.ColorPalette;
        Cover = artistUser.Artist.Cover ?? artistUser.Artist.Images
            .FirstOrDefault()?.FilePath;
        Cover = Cover is not null 
            ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = artistUser.Artist.Disambiguation;
        Description = artistUser.Artist.Description;
        Folder = artistUser.Artist.Folder ?? "";
        Id = artistUser.Artist.Id.ToString();
        LibraryId = artistUser.Artist.LibraryId;
        Name = artistUser.Artist.Name;
        Type = "artist";
        Link = new($"/music/artist/{Id}", UriKind.Relative);

        Tracks = artistUser.Artist.ArtistTrack
            .DistinctBy(artistTrack => artistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(AlbumUser playlist)
    {
        ColorPalette = playlist.Album.ColorPalette;
        Cover = playlist.Album.Cover is not null 
            ? new Uri($"/images/music{playlist.Album.Cover}", UriKind.Relative).ToString() 
            : null;
        Disambiguation = playlist.Album.Disambiguation;
        Description = playlist.Album.Description;
        Folder = playlist.Album.Folder ?? "";
        Id = playlist.Album.Id.ToString();
        LibraryId = playlist.Album.LibraryId;
        Name = playlist.Album.Name;
        Type = "album";
        Link = new($"/music/album/{Id}", UriKind.Relative);

        Tracks = playlist.Album.AlbumTrack
            .DistinctBy(albumTrack => albumTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Playlist playlist)
    {
        ColorPalette = playlist.ColorPalette;
        Cover = playlist.Cover is not null 
            ? new Uri($"/images/music{playlist.Cover}", UriKind.Relative).ToString() 
            : null;
        Description = playlist.Description;
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Type = "playlist";
        Link = new($"/music/playlists/{Id}", UriKind.Relative);

        Tracks = playlist.Tracks
            .DistinctBy(playlistTrack => playlistTrack.Track.Name.ToLower())
            .Count();
    }

    public CarouselResponseItemDto(Track track)
    {
        ColorPalette = track.ColorPalette;
        Cover = track.Cover is not null
            ? new Uri($"/images/music{track.Cover}", UriKind.Relative).ToString()
            : null;
        Folder = track.Folder ?? "";
        Id = track.Id.ToString();
        Name = track.Name;
        Type = "track";
        Link = new($"/music/tracks/{Id}", UriKind.Relative);
    }

    public CarouselResponseItemDto(MusicGenre genre)
    {
        Id = genre.Id.ToString();
        Name = genre.Name.ToTitleCase();
        Type = "genre";
        Link = new($"/music/genres/{Id}", UriKind.Relative);

        Tracks = genre.MusicGenreTracks.Count;
    }
}