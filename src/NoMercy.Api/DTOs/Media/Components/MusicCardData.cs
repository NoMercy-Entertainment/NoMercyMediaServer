using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;
using CarouselResponseItemDtoRepository = NoMercy.Data.Repositories.CarouselResponseItemDto;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMMusicCard component - music album/artist card.
/// </summary>
public record MusicCardData
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("link")] public string Link { get; set; } = null!;
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool? Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("libraryID")] public string? LibraryId { get; set; }
    [JsonProperty("trackID")] public string? TrackId { get; set; }
    [JsonProperty("tracks")] public long? Tracks { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }

    public MusicCardData()
    {
    }

    public MusicCardData(Album album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = $"/images/music{album.Cover}";
        Type = "album";
        Link = $"/music/album/{album.Id}";
        ColorPalette = album.ColorPalette;
        Year = album.Year;
        Tracks = album.AlbumTrack.Count;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = $"/images/music{artist.Cover}";
        Type = "artist";
        Link = $"/music/artist/{artist.Id}";
        ColorPalette = artist.ColorPalette;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.ArtistTrack.Count;
        LibraryId = artist.LibraryId.ToString();
    }

    public MusicCardData(Playlist playlist)
    {
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Cover = $"/images/music{playlist.Cover}";
        Type = "playlist";
        Link = $"/music/playlist/{playlist.Id}";
        Tracks = playlist.Tracks.Count;
    }

    public MusicCardData(Track track)
    {
        Id = track.Id.ToString();
        Name = track.Name;
        Type = "track";
        Link = $"/music/tracks/{track.Id}";
        Tracks = 1;
        TrackId = track.Id.ToString();
    }

    public MusicCardData(CarouselResponseItemDto carousel)
    {
        Id = carousel.Id;
        Name = carousel.Name;
        Cover = carousel.Cover;
        Type = carousel.Type;
        Link = carousel.Link.ToString();
        Tracks = carousel.Tracks;
        ColorPalette = carousel.ColorPalette;
    }

    public MusicCardData(CarouselResponseItemDtoRepository carousel)
    {
        Id = carousel.Id;
        Name = carousel.Name.ToTitleCase();
        Cover = carousel.Cover;
        Type = carousel.Type;
        Link = carousel.Link.ToString();
        Tracks = carousel.Tracks;
        ColorPalette = carousel.ColorPalette;
    }

    public MusicCardData(ArtistCardDto artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = artist.Cover is not null ? $"/images/music{artist.Cover}" : null;
        Type = "artist";
        Link = $"/music/artist/{artist.Id}";
        ColorPalette = !string.IsNullOrEmpty(artist.ColorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(artist.ColorPalette)
            : null;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.TrackCount;
        LibraryId = artist.LibraryId?.ToString();
    }

    public MusicCardData(AlbumCardDto album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = album.Cover is not null ? $"/images/music{album.Cover}" : null;
        Type = "album";
        Link = $"/music/album/{album.Id}";
        ColorPalette = !string.IsNullOrEmpty(album.ColorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(album.ColorPalette)
            : null;
        Year = album.Year;
        Tracks = album.TrackCount;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicCardData(PlaylistCardDto playlist)
    {
        Id = playlist.Id.ToString();
        Name = playlist.Name;
        Cover = playlist.Cover is not null ? $"/images/music{playlist.Cover}" : null;
        Type = "playlist";
        Link = $"/music/playlist/{playlist.Id}";
        ColorPalette = !string.IsNullOrEmpty(playlist.ColorPalette)
            ? JsonConvert.DeserializeObject<IColorPalettes>(playlist.ColorPalette)
            : null;
        Tracks = playlist.TrackCount;
    }

    public MusicCardData(MusicGenreCardDto genre)
    {
        Id = genre.Id.ToString();
        Name = genre.Name.ToTitleCase();
        Type = "genre";
        Link = $"/music/genres/{genre.Id}";
        Tracks = genre.TrackCount;
    }
}

/// <summary>
/// Data for NMMusicHomeCard component - music home featured card (similar to MusicCardData).
/// </summary>
public record MusicHomeCardData
{
    [JsonProperty("id")] public string Id { get; set; } = null!;
    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("link")] public string Link { get; set; } = null!;
    [JsonProperty("type")] public string? Type { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("favorite")] public bool? Favorite { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("libraryID")] public string? LibraryId { get; set; }
    [JsonProperty("trackID")] public string? TrackId { get; set; }
    [JsonProperty("tracks")] public long? Tracks { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }

    public MusicHomeCardData()
    {
    }

    public MusicHomeCardData(Album album)
    {
        Id = album.Id.ToString();
        Name = album.Name;
        Cover = $"/images/music{album.Cover}";
        Type = "album";
        Link = $"/music/album/{album.Id}";
        ColorPalette = album.ColorPalette;
        Year = album.Year;
        Tracks = album.AlbumTrack.Count;
        LibraryId = album.LibraryId.ToString();
    }

    public MusicHomeCardData(Artist artist)
    {
        Id = artist.Id.ToString();
        Name = artist.Name;
        Cover = $"/images/music{artist.Cover}";
        Type = "artist";
        Link = $"/music/artist/{artist.Id}";
        ColorPalette = artist.ColorPalette;
        Disambiguation = artist.Disambiguation;
        Description = artist.Description;
        Tracks = artist.ArtistTrack.Count;
        LibraryId = artist.LibraryId.ToString();
    }

    public MusicHomeCardData(TopMusicDto topMusic)
    {
        Id = topMusic.Id;
        Name = topMusic.Name;
        Cover = topMusic.Cover;
        Type = topMusic.Type;
        Link = topMusic.Link.ToString();
        ColorPalette = topMusic.ColorPalette;
    }
}
