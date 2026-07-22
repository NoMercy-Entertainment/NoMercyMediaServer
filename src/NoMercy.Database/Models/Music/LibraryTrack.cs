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

[PrimaryKey(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(TrackId))]
[Index(propertyName: nameof(LibraryId))]
[Index(propertyName: nameof(TrackId))]
public class LibraryTrack
{
    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty(propertyName: "track_id")]
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;

    public LibraryTrack()
    {
        //
    }

    public LibraryTrack(Ulid libraryId, Guid trackId)
    {
        LibraryId = libraryId;
        TrackId = trackId;
    }
}
