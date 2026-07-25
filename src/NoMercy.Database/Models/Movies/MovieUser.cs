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

[PrimaryKey(nameof(MovieId), nameof(UserId))]
[Index(nameof(MovieId))]
[Index(nameof(UserId))]
public class MovieUser
{
    [JsonProperty("movie_id")]
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public MovieUser()
    {
        //
    }

    public MovieUser(int movieId, Guid userId)
    {
        MovieId = movieId;
        UserId = userId;
    }
}
