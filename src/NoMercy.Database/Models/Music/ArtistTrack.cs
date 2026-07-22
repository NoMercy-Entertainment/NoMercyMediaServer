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

[PrimaryKey(propertyName: nameof(ArtistId), additionalPropertyNames: nameof(TrackId))]
[Index(propertyName: nameof(ArtistId))]
[Index(propertyName: nameof(TrackId))]
public class ArtistTrack
{
    [JsonProperty(propertyName: "artist_id")]
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [JsonProperty(propertyName: "track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public ArtistTrack() { }

    public ArtistTrack(Guid artistId, Guid trackId)
    {
        ArtistId = artistId;
        TrackId = trackId;
    }
}
