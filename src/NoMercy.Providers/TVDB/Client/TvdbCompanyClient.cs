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

using NoMercy.Providers.TVDB.Models.Companies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Client;

public class TvdbCompanyClient : TvdbBaseClient
{
    public TvdbCompanyClient(int id = 0, string language = "eng")
        : base(id: id, language: language) { }

    public Task<TvdbPaginatedResponse<TvdbCompany>?> All(int page = 0, bool? priority = false)
    {
        Dictionary<string, string?> query = new() { [key: "page"] = page.ToString() };
        return Get<TvdbPaginatedResponse<TvdbCompany>>(url: "companies", query: query, priority: priority);
    }

    public Task<TvdbCompanyResponse?> Details(bool? priority = false)
    {
        return Get<TvdbCompanyResponse>(url: "companies/" + Id, priority: priority);
    }

    public Task<TvdbCompanyTypesResponse?> Types(bool? priority = false)
    {
        return Get<TvdbCompanyTypesResponse>(url: "companies/types", priority: priority);
    }
}
