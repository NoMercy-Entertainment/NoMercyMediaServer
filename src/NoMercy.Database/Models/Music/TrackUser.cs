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

[PrimaryKey(propertyName: nameof(TrackId), additionalPropertyNames: nameof(UserId))]
[Index(propertyName: nameof(TrackId))]
[Index(propertyName: nameof(UserId))]
public class TrackUser
{
    [JsonProperty(propertyName: "track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public TrackUser()
    {
        //
    }

    public TrackUser(Guid trackId, Guid userId)
    {
        TrackId = trackId;
        UserId = userId;
    }
}
