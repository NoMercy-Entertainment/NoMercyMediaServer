// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Id), IsUnique = true)]
[Index(propertyName: nameof(Title))]
[Index(propertyName: nameof(Type))]
[Index(propertyName: nameof(Order))]
public class Library : Timestamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "chapter_images")]
    public bool ChapterImages { get; set; }

    [JsonProperty(propertyName: "extract_chapters")]
    public bool ExtractChapters { get; set; }

    [JsonProperty(propertyName: "extract_chapters_during")]
    public bool ExtractChaptersDuring { get; set; }

    [JsonProperty(propertyName: "image")]
    public string? Image { get; set; }

    [JsonProperty(propertyName: "auto_refresh_interval")]
    public int AutoRefreshInterval { get; set; }

    [Column(name: "AutoEncodeOnScan")]
    [JsonProperty(propertyName: "auto_encode_on_scan")]
    public bool AutoEncodeOnScan { get; set; }

    [Column(name: "EncodePresetId")]
    [JsonProperty(propertyName: "encode_preset_id")]
    public Ulid? EncodePresetId { get; set; }

    [JsonProperty(propertyName: "order")]
    public int? Order { get; set; }

    [JsonProperty(propertyName: "perfect_subtitle_match")]
    public bool PerfectSubtitleMatch { get; set; }

    [JsonProperty(propertyName: "realtime")]
    public bool Realtime { get; set; }

    [JsonProperty(propertyName: "special_season_name")]
    public string? SpecialSeasonName { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = [];

    [JsonProperty(propertyName: "language_libraries")]
    public ICollection<LanguageLibrary> LanguageLibraries { get; set; } = [];

    [JsonProperty(propertyName: "library_users")]
    public ICollection<LibraryUser> LibraryUsers { get; set; } = [];

    [JsonProperty(propertyName: "library_tvs")]
    public ICollection<LibraryTv> LibraryTvs { get; set; } = [];

    [JsonProperty(propertyName: "library_movies")]
    public ICollection<LibraryMovie> LibraryMovies { get; set; } = [];

    [JsonProperty(propertyName: "library_tracks")]
    public ICollection<LibraryTrack> LibraryTracks { get; set; } = [];

    [JsonProperty(propertyName: "collection_libraries")]
    public ICollection<CollectionLibrary> CollectionLibraries { get; set; } = [];

    [JsonProperty(propertyName: "album_libraries")]
    public ICollection<AlbumLibrary> AlbumLibraries { get; set; } = [];

    [JsonProperty(propertyName: "artist_libraries")]
    public ICollection<ArtistLibrary> ArtistLibraries { get; set; } = [];

    [JsonProperty(propertyName: "playback_preferences")]
    public ICollection<PlaybackPreference> PlaybackPreferences { get; set; } = [];
}
