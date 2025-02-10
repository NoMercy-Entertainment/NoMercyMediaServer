
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class Artist : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; } = null!;
    [JsonProperty("disambiguation")] public string? Disambiguation { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("country")] public string? Country { get; set; }
    [JsonProperty("year")] public int? Year { get; set; }

    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("host_folder")] public string HostFolder { get; set; } = null!;

    [JsonProperty("library_id")] public Ulid? LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("folder_id")] public Ulid? FolderId { get; set; }
    public Folder LibraryFolder { get; set; } = null!;

    [JsonProperty("artist_track")] public ICollection<ArtistTrack> ArtistTrack { get; set; } = [];
    [JsonProperty("album_artist")] public ICollection<AlbumArtist> AlbumArtist { get; set; } = [];
    [JsonProperty("artist_user")] public ICollection<ArtistUser> ArtistUser { get; set; } = [];
    [JsonProperty("artist_genre")] public ICollection<ArtistMusicGenre> ArtistMusicGenre { get; set; } = [];
    [JsonProperty("artist_release")] public ICollection<ArtistReleaseGroup> ArtistReleaseGroup { get; set; } = [];
    [JsonProperty("translations")] public ICollection<Translation> Translations { get; set; } = [];
    [JsonProperty("images")] public ICollection<Image> Images { get; set; } = [];

    public Artist()
    {
    }

    public Artist(string name, string folder, Ulid libraryId)
    {
        Name = name;
        Folder = folder;
        LibraryId = libraryId;
    }
}
