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

using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Repositories;

public record HomeParallelData(
    HashSet<UserData> ContinueWatching,
    List<GenreHomeDto> GenreItems,
    List<Library> Libraries,
    int AnimeCount,
    int MovieCount,
    int TvCount
);

public record HomeTvsAndMoviesData(List<HomeTvCardDto> TvData, List<HomeMovieCardDto> MovieData);

public interface IHomeRepository
{
    Task<List<Genre>> GetHome(Guid userId, string? language, int take, int page = 0);

    Task<List<HomeTvCardDto>> GetHomeTvs(
        List<int> tvIds,
        string? language,
        string country,
        CancellationToken ct = default
    );

    Task<List<HomeMovieCardDto>> GetHomeMovies(
        List<int> movieIds,
        string? language,
        string country,
        CancellationToken ct = default
    );

    Task<HashSet<UserData>> GetContinueWatchingAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<HashSet<Image>> GetScreensaverImagesAsync(Guid userId, CancellationToken ct = default);

    Task<List<Library>> GetLibrariesAsync(Guid userId, CancellationToken ct = default);

    Task<int> GetAnimeCountAsync(Guid userId, CancellationToken ct = default);

    Task<int> GetMovieCountAsync(Guid userId, CancellationToken ct = default);

    Task<int> GetTvCountAsync(Guid userId, CancellationToken ct = default);

    Task<List<GenreHomeDto>> GetHomeGenresAsync(
        Guid userId,
        string? language,
        int take,
        int page = 0,
        CancellationToken ct = default
    );

    Task<HomeParallelData> GetHomeParallelDataAsync(
        Guid userId,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<HomeTvsAndMoviesData> GetHomeTvsAndMoviesAsync(
        List<int> tvIds,
        List<int> movieIds,
        string language,
        string country,
        CancellationToken ct = default
    );
}
