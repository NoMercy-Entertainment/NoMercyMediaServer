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

using NoMercy.Providers.TMDB.Models.Configuration;

// ReSharper disable All

namespace NoMercy.Providers.TMDB.Client;

public class TmdbConfigClient : TmdbBaseClient
{
    public Task<TmdbConfiguration?> Configuration()
    {
        return Get<TmdbConfiguration>(url: "configuration");
    }

    public Task<List<TmdbLanguage>?> Languages()
    {
        return Get<List<TmdbLanguage>>(url: "configuration/languages");
    }

    public Task<List<TmdbCountry>?> Countries()
    {
        return Get<List<TmdbCountry>>(url: "configuration/countries");
    }

    public Task<List<TmdbJob>?> Jobs()
    {
        return Get<List<TmdbJob>>(url: "configuration/jobs");
    }

    public Task<List<string>?> PrimaryTranslations()
    {
        return Get<List<string>>(url: "configuration/primary_translations");
    }

    public Task<List<TmdbTimezone>?> Timezones()
    {
        return Get<List<TmdbTimezone>>(url: "configuration/timezones");
    }
}
