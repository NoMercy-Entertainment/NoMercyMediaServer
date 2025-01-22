using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;
public record AlbumsResponseTrackDto
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
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("album_track")] public List<AlbumDto> Album { get; set; }
    [JsonProperty("artist_track")] public List<ArtistDto> Artist { get; set; }

    public AlbumsResponseTrackDto(AlbumTrack artistTrack, Ulid libraryId, string country)
    {
        ColorPalette = artistTrack.Track.ColorPalette;
        Cover = artistTrack.Track.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Date = artistTrack.Track.Date;
        Disc = artistTrack.Track.DiscNumber;
        Duration = artistTrack.Track.Duration;
        Favorite = artistTrack.Track.TrackUser.Any();
        Filename = artistTrack.Track.Filename;
        Folder = artistTrack.Track.Folder;
        Id = artistTrack.Track.Id;
        LibraryId = libraryId;
        Name = artistTrack.Track.Name;
        Origin = NmSystem.Info.DeviceId;
        Path = artistTrack.Track.Folder + "/" + artistTrack.Track.Filename;
        Quality = artistTrack.Track.Quality;
        Track = artistTrack.Track.TrackNumber;
        Type = "track";
        Link = new($"/music/album/{artistTrack.AlbumId}", UriKind.Relative);

        Album = artistTrack.Track.AlbumTrack
            .Select(albumTrack => new AlbumDto(albumTrack, country))
            .ToList();

        Artist = artistTrack.Track.ArtistTrack
            .Select(trackArtist => new ArtistDto(trackArtist, country))
            .ToList();
    }
}
