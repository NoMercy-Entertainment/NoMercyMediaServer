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

[PrimaryKey(nameof(Id))]
[Index(nameof(UserId))]
[Index(nameof(TrackId))]
public class MusicPlay : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [JsonProperty("track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public MusicPlay()
    {
        //
    }

    public MusicPlay(Guid userId, Guid trackId)
    {
        UserId = userId;
        TrackId = trackId;
    }
}
