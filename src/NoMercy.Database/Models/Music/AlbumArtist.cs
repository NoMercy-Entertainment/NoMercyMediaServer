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

[PrimaryKey(nameof(AlbumId), nameof(ArtistId))]
[Index(nameof(AlbumId))]
[Index(nameof(ArtistId))]
public class AlbumArtist
{
    [JsonProperty("album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("artist_id")]
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    public AlbumArtist() { }

    public AlbumArtist(Guid albumId, Guid artistId)
    {
        AlbumId = albumId;
        ArtistId = artistId;
    }
}
