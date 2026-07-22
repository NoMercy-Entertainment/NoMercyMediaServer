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

[PrimaryKey(propertyName: nameof(CollectionId), additionalPropertyNames: nameof(UserId))]
[Index(propertyName: nameof(CollectionId))]
[Index(propertyName: nameof(UserId))]
public class CollectionUser
{
    [JsonProperty(propertyName: "collection_id")]
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public CollectionUser() { }

    public CollectionUser(int collectionId, Guid userId)
    {
        CollectionId = collectionId;
        UserId = userId;
    }
}
