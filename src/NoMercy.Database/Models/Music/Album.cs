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

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Name))]
[Index(propertyName: nameof(TitleSort))]
[Index(propertyName: nameof(LibraryId))]
[Index(propertyName: nameof(FolderId))]
[Index(propertyName: nameof(Year))]
[Index(propertyName: nameof(MetadataId))]
public class Album : ColorPaletteTimeStamps, IHasLibrary
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [MaxLength(length: 4096)]
    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "country")]
    public string? Country { get; set; }

    [JsonProperty(propertyName: "year")]
    public int Year { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "host_folder")]
    public string HostFolder
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = new();

    [JsonProperty(propertyName: "folder_id")]
    public Ulid FolderId { get; set; }
    public Folder LibraryFolder { get; set; } = new();

    [JsonProperty(propertyName: "metadata_id")]
    public Ulid? MetadataId { get; set; }
    public Metadata? Metadata { get; init; }

    [JsonProperty(propertyName: "album_track")]
    public ICollection<AlbumTrack> AlbumTrack { get; set; } = [];

    [JsonProperty(propertyName: "album_artist")]
    public ICollection<AlbumArtist> AlbumArtist { get; set; } = [];

    [JsonProperty(propertyName: "album_user")]
    public ICollection<AlbumUser> AlbumUser { get; set; } = [];

    [JsonProperty(propertyName: "album_genre")]
    public ICollection<AlbumMusicGenre> AlbumMusicGenre { get; set; } = [];

    [JsonProperty(propertyName: "album_release")]
    public ICollection<AlbumReleaseGroup> AlbumReleaseGroup { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public ICollection<Translation> Translations { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];
}
