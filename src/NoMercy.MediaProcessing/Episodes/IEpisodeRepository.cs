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

using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.Episodes;

public interface IEpisodeRepository
{
    public Task StoreEpisodes(IEnumerable<Episode> episodes);
    public Task StoreEpisodeTranslations(List<Translation> translations);
    public Task StoreEpisodeImages(IEnumerable<Image> images);
}
