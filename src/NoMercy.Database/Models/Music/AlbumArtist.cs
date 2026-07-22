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

[PrimaryKey(propertyName: nameof(AlbumId), additionalPropertyNames: nameof(ArtistId))]
[Index(propertyName: nameof(AlbumId))]
[Index(propertyName: nameof(ArtistId))]
public class AlbumArtist
{
    [JsonProperty(propertyName: "album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty(propertyName: "artist_id")]
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    public AlbumArtist() { }

    public AlbumArtist(Guid albumId, Guid artistId)
    {
        AlbumId = albumId;
        ArtistId = artistId;
    }
}
