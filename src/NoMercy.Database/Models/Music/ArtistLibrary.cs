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

[PrimaryKey(propertyName: nameof(ArtistId), additionalPropertyNames: nameof(LibraryId))]
[Index(propertyName: nameof(ArtistId))]
[Index(propertyName: nameof(LibraryId))]
public class ArtistLibrary
{
    [JsonProperty(propertyName: "artist_id")]
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public ArtistLibrary() { }

    public ArtistLibrary(Guid artistId, Ulid libraryId)
    {
        ArtistId = artistId;
        LibraryId = libraryId;
    }
}
