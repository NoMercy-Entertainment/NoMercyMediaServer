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

[PrimaryKey(nameof(GenreId), nameof(ReleaseGroupId))]
[Index(nameof(GenreId))]
[Index(nameof(ReleaseGroupId))]
public class MusicGenreReleaseGroup
{
    [JsonProperty("genre_id")]
    public Guid GenreId { get; set; }
    public MusicGenre Genre { get; set; } = null!;

    [JsonProperty("track_id")]
    public Guid ReleaseGroupId { get; set; }
    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public MusicGenreReleaseGroup()
    {
        //
    }

    public MusicGenreReleaseGroup(Guid genreId, Guid trackId)
    {
        GenreId = genreId;
        ReleaseGroupId = trackId;
    }
}
