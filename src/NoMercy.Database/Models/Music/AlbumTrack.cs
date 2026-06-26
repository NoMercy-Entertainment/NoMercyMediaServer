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

[PrimaryKey(nameof(AlbumId), nameof(TrackId))]
[Index(nameof(AlbumId))]
[Index(nameof(TrackId))]
public class AlbumTrack
{
    [JsonProperty("album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public AlbumTrack() { }

    public AlbumTrack(Guid albumId, Guid trackId)
    {
        AlbumId = albumId;
        TrackId = trackId;
    }
}
