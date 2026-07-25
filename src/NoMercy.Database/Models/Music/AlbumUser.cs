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

[PrimaryKey(nameof(AlbumId), nameof(UserId))]
[Index(nameof(AlbumId))]
[Index(nameof(UserId))]
public class AlbumUser
{
    [JsonProperty("album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public AlbumUser() { }

    public AlbumUser(Guid albumId, Guid userId)
    {
        AlbumId = albumId;
        UserId = userId;
    }
}
