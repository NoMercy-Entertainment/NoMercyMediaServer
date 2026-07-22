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

[PrimaryKey(propertyName: nameof(AlbumId), additionalPropertyNames: nameof(ReleaseGroupId))]
[Index(propertyName: nameof(AlbumId))]
[Index(propertyName: nameof(ReleaseGroupId))]
public class AlbumReleaseGroup
{
    [JsonProperty(propertyName: "album_id")]
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty(propertyName: "release_id")]
    public Guid ReleaseGroupId { get; set; }
    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public AlbumReleaseGroup() { }

    public AlbumReleaseGroup(Guid albumId, Guid releaseId)
    {
        AlbumId = albumId;
        ReleaseGroupId = releaseId;
    }
}
