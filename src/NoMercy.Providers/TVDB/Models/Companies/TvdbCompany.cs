using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Companies;

public class TvdbCompaniesResponse : TvdbResponse<TvdbCompany[]> { }

public class TvdbCompanyResponse : TvdbResponse<TvdbCompany> { }

public class TvdbCompanyTypesResponse : TvdbResponse<TvdbCompanyType[]> { }

public class TvdbCompany
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("activeDate")]
    public string? ActiveDate { get; set; }

    [JsonProperty("inactiveDate")]
    public string? InactiveDate { get; set; }

    [JsonProperty("primaryCompanyType")]
    public int PrimaryCompanyType { get; set; }

    [JsonProperty("aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("parentCompany")]
    public TvdbParentCompany? ParentCompany { get; set; }

    [JsonProperty("tagOptions")]
    public TvdbTagOption[] TagOptions { get; set; } = [];

    [JsonProperty("status")]
    public string? Status { get; set; }
}

public class TvdbParentCompany
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("relation")]
    public TvdbCompanyRelation? Relation { get; set; }
}

public class TvdbCompanyRelation
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("typeName")]
    public string TypeName { get; set; } = string.Empty;
}

public class TvdbCompanyType
{
    [JsonProperty("companyTypeId")]
    public int CompanyTypeId { get; set; }

    [JsonProperty("companyTypeName")]
    public string CompanyTypeName { get; set; } = string.Empty;
}
