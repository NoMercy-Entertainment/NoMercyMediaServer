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

using Newtonsoft.Json;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Api.DTOs.Media;

public class CompanyDto
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("headquarters")]
    public string? Headquarters { get; set; }

    [JsonProperty("link")]
    public Uri? Homepage { get; set; }

    [JsonProperty("logo")]
    public string? Logo { get; set; }

    [JsonProperty("origin_country")]
    public string? OriginCountry { get; set; }

    [JsonProperty("parent_company")]
    public int? ParentCompany { get; set; }

    public CompanyDto() { }

    public CompanyDto(CompanyTv ctv)
    {
        Id = ctv.Company.Id;
        Name = ctv.Company.Name;
        Description = ctv.Company.Description;
        Headquarters = ctv.Company.Headquarters;
        Homepage = ctv.Company.Homepage;
        Logo = ctv.Company.Logo;
        OriginCountry = ctv.Company.OriginCountry;
        ParentCompany = ctv.Company.ParentCompany;
    }

    public CompanyDto(TmdbProductionCompany ctv)
    {
        Id = ctv.Id;
        Name = ctv.Name;
        Description = null;
        Headquarters = null;
        Logo = ctv.LogoPath;
        OriginCountry = ctv.OriginCountry;
        ParentCompany = null;
    }

    public CompanyDto(CompanyMovie ctv)
    {
        Id = ctv.Company.Id;
        Name = ctv.Company.Name;
        Description = ctv.Company.Description;
        Headquarters = ctv.Company.Headquarters;
        Homepage = ctv.Company.Homepage;
        Logo = ctv.Company.Logo;
        OriginCountry = ctv.Company.OriginCountry;
        ParentCompany = ctv.Company.ParentCompany;
    }
}
