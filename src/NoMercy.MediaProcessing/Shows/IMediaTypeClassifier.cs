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
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

/// <summary>
/// Classifies a TMDB show into a NoMercy media type (e.g. "anime" or "tv").
/// Extracted from ShowRepository so the data layer no longer performs outbound
/// HTTP calls; the classification provider is injected and independently testable.
/// </summary>
public interface IMediaTypeClassifier
{
    Task<string> ClassifyAsync(TmdbTvShowAppends show);

    Task<string> ClassifyAsync(string name, int? year);
}
