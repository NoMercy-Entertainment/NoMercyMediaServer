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

using NoMercy.Database.Models.People;

namespace NoMercy.Data.Repositories;

public interface IPeopleRepository
{
    Task<List<Person>> GetPeopleAsync(
        Guid userId,
        string language,
        int take,
        int page = 0,
        CancellationToken ct = default
    );

    Task<Person?> GetPersonWithCreditsAsync(int id, CancellationToken ct = default);
}
