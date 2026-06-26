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

[PrimaryKey(nameof(AlbumId), nameof(ReleaseGroupId))]
[Index(nameof(AlbumId))]
[Index(nameof(ReleaseGroupId))]
public class AlbumReleaseGroup
{
    [JsonProperty("album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("release_id")]
    public Guid ReleaseGroupId { get; set; }
    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public AlbumReleaseGroup() { }

    public AlbumReleaseGroup(Guid albumId, Guid releaseId)
    {
        AlbumId = albumId;
        ReleaseGroupId = releaseId;
    }
}
