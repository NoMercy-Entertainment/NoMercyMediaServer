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

public class RecommendationDiagnosticsDto
{
    public List<string> Libraries { get; set; } = [];
    public int AnimeByLibraryType { get; set; }
    public int AnimeByMediaType { get; set; }
    public int TotalRecsWithTv { get; set; }
    public int AnimeRecsByMediaType { get; set; }
    public int TotalSimWithTv { get; set; }
    public int AnimeSimByMediaType { get; set; }
    public List<int> SampleAnimeIds { get; set; } = [];
    public int SampleRecsCount { get; set; }
}
