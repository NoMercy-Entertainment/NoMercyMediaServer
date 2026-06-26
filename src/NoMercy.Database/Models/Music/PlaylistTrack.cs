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

[PrimaryKey(nameof(PlaylistId), nameof(TrackId))]
[Index(nameof(PlaylistId))]
[Index(nameof(TrackId))]
public class PlaylistTrack
{
    [JsonProperty("playlist_id")]
    public Guid PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    [JsonProperty("track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public PlaylistTrack()
    {
        //
    }

    public PlaylistTrack(Guid playlistId, Guid trackId)
    {
        PlaylistId = playlistId;
        TrackId = trackId;
    }
}
