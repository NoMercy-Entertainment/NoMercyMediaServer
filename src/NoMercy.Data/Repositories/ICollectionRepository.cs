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

using NoMercy.Database.Models.Movies;

namespace NoMercy.Data.Repositories;

public interface ICollectionRepository
{
    Task<List<Collection>> GetCollectionsAsync(
        Guid userId,
        string language,
        int take,
        int page,
        CancellationToken ct = default
    );

    Task<List<CollectionListDto>> GetCollectionsListAsync(
        Guid userId,
        string language,
        string country,
        int take,
        int page,
        CancellationToken ct = default
    );

    Task<Collection?> GetCollectionAsync(
        Guid userId,
        int id,
        string? language,
        string country,
        CancellationToken ct = default
    );

    Task<List<CollectionListDto>> GetCollectionItemCardsAsync(
        Guid userId,
        string? language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    );

    Task<List<Collection>> GetCollectionItems(
        Guid userId,
        string? language,
        string country,
        int take = 1,
        int page = 0,
        CancellationToken ct = default
    );

    Task<Collection?> GetAvailableCollectionAsync(
        Guid userId,
        int id,
        CancellationToken ct = default
    );

    Task<Collection?> GetCollectionPlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    );

    Task<bool> LikeAsync(int id, Guid userId, bool like, CancellationToken ct = default);

    Task<bool> AddToWatchListAsync(
        int collectionId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    );

    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<Collection?> GetCollectionForRescanAsync(int id, CancellationToken ct = default);

    Task<Collection?> GetCollectionWithMovieLibrariesAsync(int id, CancellationToken ct = default);
}
