using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
[Index(nameof(Title))]
[Index(nameof(Type))]
[Index(nameof(Order))]
public class Library : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("auto_refresh_interval")]
    public bool ChapterImages { get; set; }

    [JsonProperty("chapter_images")] public bool ExtractChapters { get; set; }
    [JsonProperty("extract_chapters")] public bool ExtractChaptersDuring { get; set; }
    [JsonProperty("image")] public string? Image { get; set; }
    [JsonProperty("name")] public int AutoRefreshInterval { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }

    [JsonProperty("perfect_subtitle_match")]
    public bool PerfectSubtitleMatch { get; set; }

    [JsonProperty("realtime")] public bool Realtime { get; set; }
    [JsonProperty("special_season_name")] public string? SpecialSeasonName { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = new List<FolderLibrary>();

    [JsonProperty("language_libraries")]
    public ICollection<LanguageLibrary> LanguageLibraries { get; set; } = new List<LanguageLibrary>();

    [JsonProperty("library_users")]
    public ICollection<LibraryUser> LibraryUsers { get; set; } = new List<LibraryUser>();

    [JsonProperty("library_tvs")] public ICollection<LibraryTv> LibraryTvs { get; set; } = new List<LibraryTv>();

    [JsonProperty("library_movies")]
    public ICollection<LibraryMovie> LibraryMovies { get; set; } = new List<LibraryMovie>();

    [JsonProperty("library_tracks")]
    public ICollection<LibraryTrack> LibraryTracks { get; set; } = new List<LibraryTrack>();

    [JsonProperty("collection_libraries")]
    public ICollection<CollectionLibrary> CollectionLibraries { get; set; } = new List<CollectionLibrary>();

    [JsonProperty("album_libraries")]
    public ICollection<AlbumLibrary> AlbumLibraries { get; set; } = new List<AlbumLibrary>();

    [JsonProperty("artist_libraries")]
    public ICollection<ArtistLibrary> ArtistLibraries { get; set; } = new List<ArtistLibrary>();
}