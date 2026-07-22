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
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Data.Repositories;

public class LanguageRepository(MediaContext context) : ILanguageRepository
{
    public async Task<List<Language>> GetLanguagesAsync()
    {
        return await context.Languages.AsNoTracking().ToListAsync();
    }

    public Task<List<Country>> GetCountriesAsync(CancellationToken ct = default)
    {
        return context.Countries.AsNoTracking().ToListAsync(cancellationToken: ct);
    }

    public Task<List<LanguageLibrary>> GetLanguagesAsync(string[] list)
    {
        return context
            .LanguageLibrary.Where(predicate: language => list.Contains(language.Language.Iso6391))
            .ToListAsync();
    }
}
