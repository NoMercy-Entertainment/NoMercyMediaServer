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

using NoMercy.MediaProcessing.Shows;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Tests.Repositories.Infrastructure;

/// <summary>
/// No-network test double for repository tests that construct a
/// TvShowRepository but do not exercise classification behavior.
/// </summary>
public class StubMediaTypeClassifier : IMediaTypeClassifier
{
    public Task<string?> ClassifyAsync(TmdbTvShowAppends show) => Task.FromResult<string?>("tv");

    public Task<string?> ClassifyAsync(string name, int? year, string[]? originCountry = null) =>
        Task.FromResult<string?>("tv");
}
