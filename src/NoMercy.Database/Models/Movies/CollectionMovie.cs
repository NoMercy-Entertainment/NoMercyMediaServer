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

[PrimaryKey(nameof(CollectionId), nameof(MovieId))]
[Index(nameof(CollectionId))]
[Index(nameof(MovieId))]
[Index(nameof(MovieId), nameof(CollectionId))]
public class CollectionMovie
{
    [JsonProperty("collection_id")]
    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    [JsonProperty("movie_id")]
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public CollectionMovie() { }

    public CollectionMovie(int collectionId, int movieId)
    {
        CollectionId = collectionId;
        MovieId = movieId;
    }

    // public CollectionMovie(Providers.TMDB.Models.Movies.TmdbMovie collectionId, int collectionsId)
    // {
    //     MovieId = collectionId.Id;
    //     CollectionId = collectionsId;
    // }
}
