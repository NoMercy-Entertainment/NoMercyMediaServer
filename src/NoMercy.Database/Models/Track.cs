using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Folder))]
[Index(nameof(Filename))]
// [Index(nameof(Filename), nameof(HostFolder), IsUnique = true)]
public class Track : ColorPaletteTimeStamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("track")] public int TrackNumber { get; set; }
    [JsonProperty("disc")] public int DiscNumber { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("date")] public DateTime? Date { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("quality")] public int? Quality { get; set; }

    [Column("Lyrics")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _lyrics { get; set; }

    [NotMapped]
    [JsonProperty("lyrics")]
    public Lyric[]? Lyrics
    {
        get
        {
            if (_lyrics is null) return null;
            try
            {
                return JsonConvert.DeserializeObject<Lyric[]>(_lyrics);
            }
            catch (Exception)
            {
                return _lyrics.Split("\\n")
                    .Select(l => new Lyric
                    {
                        Text = Regex.Replace(l, "^\"|\"$", "")
                    }).ToArray();
            }
        }
        set => _lyrics = JsonConvert.SerializeObject(value);
    }

    [JsonProperty("folder")] public string? Folder { get; set; }
    [JsonProperty("host_folder")] public string? HostFolder { get; set; }

    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    public Folder LibraryFolder { get; set; } = null!;
    
    [JsonProperty("metadata_id")] public int? MetadataId { get; set; }
    public Metadata Metadata { get; init; } = null!;

    [JsonProperty("album_track")] public ICollection<AlbumTrack> AlbumTrack { get; set; } = [];
    [JsonProperty("artist_track")] public ICollection<ArtistTrack> ArtistTrack { get; set; } = [];
    [JsonProperty("library_track")] public ICollection<LibraryTrack> LibraryTrack { get; set; } = [];
    [JsonProperty("playlist_track")] public ICollection<PlaylistTrack> PlaylistTrack { get; set; } = [];
    [JsonProperty("images")] public ICollection<Image> Images { get; set; } = [];
    [JsonProperty("track_user")] public ICollection<TrackUser> TrackUser { get; set; } = [];
    [JsonProperty("genre_track")] public ICollection<MusicGenreTrack> MusicGenreTrack { get; set; } = [];
    [JsonProperty("music_plays")] public ICollection<MusicPlay> MusicPlays { get; set; } = [];
}
