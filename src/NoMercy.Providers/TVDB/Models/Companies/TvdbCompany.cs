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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Companies;

public class TvdbCompaniesResponse : TvdbResponse<TvdbCompany[]> { }

public class TvdbCompanyResponse : TvdbResponse<TvdbCompany> { }

public class TvdbCompanyTypesResponse : TvdbResponse<TvdbCompanyType[]> { }

public class TvdbCompany
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty(propertyName: "country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty(propertyName: "activeDate")]
    public string? ActiveDate { get; set; }

    [JsonProperty(propertyName: "inactiveDate")]
    public string? InactiveDate { get; set; }

    [JsonProperty(propertyName: "primaryCompanyType")]
    public int PrimaryCompanyType { get; set; }

    [JsonProperty(propertyName: "aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty(propertyName: "nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty(propertyName: "overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty(propertyName: "parentCompany")]
    public TvdbParentCompany? ParentCompany { get; set; }

    [JsonProperty(propertyName: "tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty(propertyName: "status")]
    public string? Status { get; set; }
}

public class TvdbParentCompany
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "relation")]
    public TvdbCompanyRelation? Relation { get; set; }
}

public class TvdbCompanyRelation
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "typeName")]
    public string TypeName { get; set; } = string.Empty;
}

public class TvdbCompanyType
{
    [JsonProperty(propertyName: "companyTypeId")]
    public int CompanyTypeId { get; set; }

    [JsonProperty(propertyName: "companyTypeName")]
    public string CompanyTypeName { get; set; } = string.Empty;
}
