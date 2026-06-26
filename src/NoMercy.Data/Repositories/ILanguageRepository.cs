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

namespace NoMercy.Data.Repositories;

public interface ILanguageRepository
{
    Task<List<Language>> GetLanguagesAsync();

    Task<List<LanguageLibrary>> GetLanguagesAsync(string[] list);

    Task<List<Country>> GetCountriesAsync(CancellationToken ct = default);
}
