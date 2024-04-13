using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.Server.app.Helper;

namespace NoMercy.Server.app.Http.Controllers.Api.V1.Music.DTO;

public class AlbumsResponseDto
{
    [JsonProperty("data")] public IEnumerable<AlbumsResponseItemDto> Data { get; set; } = [];
    
    private static readonly string[] Letters = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];

    public static readonly Func<MediaContext, Guid, string, IAsyncEnumerable<Album>> GetAlbums =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, string letter) => mediaContext.Albums
            .AsNoTracking()

            .OrderBy(album => album.Name)

            .Where(album => letter == "_"
                ? Letters.Any(p => album.Name.StartsWith(p))
                : album.Name.StartsWith(letter)
            )
        );
}

public class AlbumsResponseItemDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("track_id")] public string? TrackId { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    
    [JsonProperty("tracks")] public int Tracks { get; set; }
    
    public AlbumsResponseItemDto(Album album)
    {
        ColorPalette = album.ColorPalette;
        Cover = album.Cover;
        Description = album.Description;
        Folder = album.Folder;
        Id = album.Id;
        Name = album.Name;
        Type = "albums";
    }
}

public class AlbumsResponseTrackDto
{
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    [JsonProperty("disc")] public int? Disc { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("id")] public Guid Id { get; set; }
    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("origin")] public Guid Origin { get; set; }
    [JsonProperty("path")] public string Path { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }
    [JsonProperty("track")] public int? Track { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    
    [JsonProperty("album_track")] public List<AlbumDto> Album { get; set; }
    [JsonProperty("artist_track")] public List<ArtistDto> Artist { get; set; }
    
    public AlbumsResponseTrackDto(AlbumTrack artistTrack, Ulid libraryId)
    {
        ColorPalette = artistTrack.Track.ColorPalette;
        Cover = artistTrack.Track.Cover;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Any();
        Filename = artistTrack.Track.Filename;
        Folder = artistTrack.Track.Folder;
        Id = artistTrack.Track.Id;
        LibraryId = libraryId;
        Name = artistTrack.Track.Name;
        Origin = SystemInfo.DeviceId;
        Path = artistTrack.Track.Folder + "/" + artistTrack.Track.Filename;
        Quality = artistTrack.Track.Quality;
        Track = artistTrack.Track.TrackNumber;
        Type = "track";
        
        Album = artistTrack.Track.AlbumTrack
            .Select(albumTrack => new AlbumDto(albumTrack))
            .ToList();
        
        Artist = artistTrack.Track.ArtistTrack
            .Select(trackArtist => new ArtistDto(trackArtist))
            .ToList();
    }
}
