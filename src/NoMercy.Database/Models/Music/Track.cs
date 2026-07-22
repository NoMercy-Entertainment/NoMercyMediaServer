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
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Infrastructure;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Database.Models.Music;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Name))]
[Index(propertyName: nameof(Folder))]
[Index(propertyName: nameof(Filename))]
[Index(propertyName: nameof(TrackNumber))]
[Index(propertyName: nameof(DiscNumber))]
// Non-unique on purpose: a unique constraint would fail to apply on existing
// libraries that already contain duplicate tracks. Speeds the dedup lookup; a
// unique constraint needs a separate de-dup migration first.
[Index(propertyName: nameof(Filename), additionalPropertyNames: nameof(HostFolder))]
public class Track : ColorPaletteTimeStamps
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "track")]
    public int TrackNumber { get; set; }

    [JsonProperty(propertyName: "disc")]
    public int DiscNumber { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "date")]
    public DateTime? Date { get; set; }

    [JsonProperty(propertyName: "filename")]
    public string? Filename { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonProperty(propertyName: "quality")]
    public int? Quality { get; set; }

    [JsonProperty(propertyName: "lyrics_offset")]
    public int? LyricsOffset { get; set; }

    [Column(name: "Lyrics")]
    [System.Text.Json.Serialization.JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _lyrics { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "lyrics")]
    public Lyric[]? Lyrics
    {
        get
        {
            if (_lyrics is null)
                return null;
            try
            {
                return JsonConvert.DeserializeObject<Lyric[]>(value: _lyrics);
            }
            catch (Exception)
            {
                return _lyrics
                    .Split(separator: "\\n")
                    .Select(selector: l => new Lyric { Text = Regex.Replace(input: l, pattern: "^\"|\"$", replacement: "") })
                    .ToArray();
            }
        }
        set => _lyrics = JsonConvert.SerializeObject(value: value);
    }

    [JsonProperty(propertyName: "folder")]
    public string? Folder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "host_folder")]
    public string? HostFolder
    {
        get;
        set => field = PathNormalizer.NormalizeNullable(value: value);
    }

    [JsonProperty(propertyName: "folder_id")]
    public Ulid FolderId { get; set; }
    public Folder LibraryFolder { get; set; } = null!;

    [JsonProperty(propertyName: "metadata_id")]
    public Ulid? MetadataId { get; set; }
    public Metadata Metadata { get; init; } = null!;

    [JsonProperty(propertyName: "album_track")]
    public ICollection<AlbumTrack> AlbumTrack { get; set; } = [];

    [JsonProperty(propertyName: "artist_track")]
    public ICollection<ArtistTrack> ArtistTrack { get; set; } = [];

    [JsonProperty(propertyName: "library_track")]
    public ICollection<LibraryTrack> LibraryTrack { get; set; } = [];

    [JsonProperty(propertyName: "playlist_track")]
    public ICollection<PlaylistTrack> PlaylistTrack { get; set; } = [];

    [JsonProperty(propertyName: "images")]
    public ICollection<Image> Images { get; set; } = [];

    [JsonProperty(propertyName: "track_user")]
    public ICollection<TrackUser> TrackUser { get; set; } = [];

    [JsonProperty(propertyName: "genre_track")]
    public ICollection<MusicGenreTrack> MusicGenreTrack { get; set; } = [];

    [JsonProperty(propertyName: "music_plays")]
    public ICollection<MusicPlay> MusicPlays { get; set; } = [];

    public string CreateFolderName()
    {
        return Name.CleanFileName();
    }

    public string CreateName()
    {
        int padding = 2;
        if (AlbumTrack.Count.ToString().Length > 2)
            padding = AlbumTrack.Count.ToString().Length;

        // Track may be orphaned during ingest (no AlbumTrack row yet) — fall
        // back to an empty album prefix instead of throwing.
        return string.Concat(values: [AlbumTrack.FirstOrDefault()?.Album.Name ?? string.Empty, ": ", DiscNumber.ToString(), "-", TrackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0'), " - ", Name, " NoMercy"]
        );
    }

    public string CreateTitle()
    {
        int padding = 2;
        if (AlbumTrack.Count.ToString().Length > 2)
            padding = AlbumTrack.Count.ToString().Length;
        return string.Concat(
            str0: TrackNumber.ToString().PadLeft(totalWidth: padding, paddingChar: '0'),
            str1: " - ",
            str2: Name.MusicBrainzSafeName(),
            str3: ".NoMercy"
        );
    }
}
