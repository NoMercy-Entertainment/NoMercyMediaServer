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
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.Repositories;

public class UserAffinitySourceDto
{
    public int ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public int? TimeWatched { get; set; }
    public int? Duration { get; set; }
    public bool IsFavorited { get; set; }
    public List<int> GenreIds { get; set; } = [];
    public List<int> KeywordIds { get; set; } = [];
}
