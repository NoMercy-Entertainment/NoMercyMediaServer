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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Music;

[PrimaryKey(propertyName: nameof(AlbumId), additionalPropertyNames: nameof(MusicGenreId))]
[Index(propertyName: nameof(AlbumId))]
[Index(propertyName: nameof(MusicGenreId))]
public class AlbumMusicGenre
{
    [JsonProperty(propertyName: "album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty(propertyName: "music_genre_id")]
    public Guid MusicGenreId { get; set; }
    public MusicGenre MusicGenre { get; set; } = null!;

    public AlbumMusicGenre() { }

    public AlbumMusicGenre(Guid albumId, Guid musicGenreId)
    {
        AlbumId = albumId;
        MusicGenreId = musicGenreId;
    }
}
