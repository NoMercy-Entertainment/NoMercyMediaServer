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

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(propertyName: nameof(LibraryId), additionalPropertyNames: nameof(UserId))]
[Index(propertyName: nameof(UserId))]
[Index(propertyName: nameof(LibraryId))]
public class LibraryUser
{
    [JsonProperty(propertyName: "library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty(propertyName: "user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public LibraryUser()
    {
        //
    }

    public LibraryUser(Ulid libraryId, Guid userId)
    {
        LibraryId = libraryId;
        UserId = userId;
    }
}
