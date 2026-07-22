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

namespace NoMercy.Database.Models.Movies;

[PrimaryKey(propertyName: nameof(CollectionId), additionalPropertyNames: nameof(LibraryId))]
[Index(propertyName: nameof(CollectionId))]
[Index(propertyName: nameof(LibraryId))]
public class CollectionLibrary
{
    [JsonProperty(propertyName: "collection_id")]
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public CollectionLibrary() { }

    public CollectionLibrary(int collectionId, Ulid libraryId)
    {
        CollectionId = collectionId;
        LibraryId = libraryId;
    }
}
