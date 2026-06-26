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

[PrimaryKey(nameof(LibraryId), nameof(MovieId))]
[Index(nameof(LibraryId))]
[Index(nameof(MovieId))]
public class LibraryMovie
{
    [JsonProperty("library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("movie_id")]
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public LibraryMovie()
    {
        //
    }

    public LibraryMovie(Ulid libraryId, int movieId)
    {
        LibraryId = libraryId;
        MovieId = movieId;
    }
}
