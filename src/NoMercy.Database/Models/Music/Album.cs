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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Infrastructure;

namespace NoMercy.Database.Models.Music;

[PrimaryKey(nameof(Id))]
[Index(nameof(Name))]
[Index(nameof(TitleSort))]
[Index(nameof(LibraryId))]
[Index(nameof(FolderId))]
[Index(nameof(Year))]
[Index(nameof(MetadataId))]
public class Album : ColorPaletteTimeStamps, IHasLibrary
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty("disambiguation")]
    public string? Disambiguation { get; set; }

    [MaxLength(4096)]
    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("cover")]
    public string? Cover { get; set; }

    [JsonProperty("country")]
    public string? Country { get; set; }

    [JsonProperty("year")]
    public int Year { get; set; }

    [JsonProperty("tracks")]
    public int Tracks { get; set; }

    [JsonProperty("folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value);
    }

    [JsonProperty("host_folder")]
    public string HostFolder
    {
        get;
        set => field = PathNormalizer.Normalize(value);
    } = string.Empty;

    [JsonProperty("library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = new();

    [JsonProperty("folder_id")]
    public Ulid FolderId { get; set; }
    public Folder LibraryFolder { get; set; } = new();

    [JsonProperty("metadata_id")]
    public Ulid? MetadataId { get; set; }
    public Metadata? Metadata { get; init; }

    [JsonProperty("album_track")]
    public ICollection<AlbumTrack> AlbumTrack { get; set; } = [];

    [JsonProperty("album_artist")]
    public ICollection<AlbumArtist> AlbumArtist { get; set; } = [];

    [JsonProperty("album_user")]
    public ICollection<AlbumUser> AlbumUser { get; set; } = [];

    [JsonProperty("album_genre")]
    public ICollection<AlbumMusicGenre> AlbumMusicGenre { get; set; } = [];

    [JsonProperty("album_release")]
    public ICollection<AlbumReleaseGroup> AlbumReleaseGroup { get; set; } = [];

    [JsonProperty("translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [JsonProperty("images")]
    public ICollection<Image> Images { get; set; } = [];
}
