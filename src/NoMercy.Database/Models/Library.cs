﻿
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Id), IsUnique = true)]
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
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("type")] public string Type { get; set; }

    [JsonProperty("folder_libraries")] public ICollection<FolderLibrary> FolderLibraries { get; set; }
    [JsonProperty("language_libraries")] public ICollection<LanguageLibrary> LanguageLibraries { get; set; }
    [JsonProperty("library_users")] public ICollection<LibraryUser> LibraryUsers { get; set; }
    [JsonProperty("file_libraries")] public ICollection<FileLibrary> FileLibraries { get; set; }
    [JsonProperty("library_tvs")] public ICollection<LibraryTv> LibraryTvs { get; set; }
    [JsonProperty("library_movies")] public ICollection<LibraryMovie> LibraryMovies { get; set; }
    [JsonProperty("library_tracks")] public ICollection<LibraryTrack> LibraryTracks { get; set; }
    [JsonProperty("collection_libraries")] public ICollection<CollectionLibrary> CollectionLibraries { get; set; }
    [JsonProperty("album_libraries")] public ICollection<AlbumLibrary> AlbumLibraries { get; set; }
    [JsonProperty("artist_libraries")] public ICollection<ArtistLibrary> ArtistLibraries { get; set; }
}
