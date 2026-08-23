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

namespace NoMercy.Data.Repositories;

public interface IAnimeSeasonRepository
{
    Task<List<AnimeSeasonWithCountsDto>> GetSeasonsWithCountsAsync(
        Guid userId,
        int take,
        int page,
        CancellationToken ct = default
    );

    Task<(
        AnimeSeasonDetailDto? Season,
        List<HomeMovieCardDto> Movies,
        List<HomeTvCardDto> TvShows
    )> GetSeasonCardsAsync(
        Guid userId,
        int id,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    );
}
