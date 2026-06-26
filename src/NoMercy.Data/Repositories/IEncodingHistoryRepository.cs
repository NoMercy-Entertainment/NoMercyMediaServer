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

namespace NoMercy.Data.Repositories;

public interface IEncodingHistoryRepository
{
    Task AddAsync(EncodingHistory entry);

    Task<List<EncodingHistory>> GetRecentAsync(int pageSize = 50, int pageIndex = 0);

    Task<int> GetTotalCountAsync();

    Task<EncodingHistory?> GetByIdAsync(Ulid id);

    Task<bool> DeleteAsync(Ulid id);

    Task<int> DeleteOlderThanAsync(DateTime olderThan);

    Task<int> DeleteAllAsync();

    Task<EncodingHistoryStats> GetAggregateStatsAsync();
}
