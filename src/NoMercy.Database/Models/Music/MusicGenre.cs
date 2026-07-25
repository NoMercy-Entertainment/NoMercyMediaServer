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

namespace NoMercy.Database.Models.Music;

[Index(nameof(Name))]
[PrimaryKey(nameof(Id))]
public class MusicGenre
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    public ICollection<AlbumMusicGenre> AlbumMusicGenres { get; set; } = [];
    public ICollection<ArtistMusicGenre> ArtistMusicGenres { get; set; } = [];
    public ICollection<MusicGenreTrack> MusicGenreTracks { get; set; } = [];
    public ICollection<MusicGenreReleaseGroup> MusicGenreReleaseGroups { get; set; } = [];

    // public MusicGenre(Providers.MusicBrainz.Models.MusicBrainzGenre musicBrainzGenre)
    // {
    //     Id = musicBrainzGenre.Id;
    //     Name = musicBrainzGenre.Name;
    // }
}
