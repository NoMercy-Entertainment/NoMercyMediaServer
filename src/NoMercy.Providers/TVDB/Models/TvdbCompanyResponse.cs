using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbCompaniesResponse : TvdbResponse<TvdbCompany[]>
{
}

public class TvdbCompanyResponse : TvdbResponse<TvdbCompany>
{
}

public class TvdbCompany
{
    [JsonProperty("activeDate")] public string ActiveDate { get; set; } = string.Empty;
    [JsonProperty("aliases")] public TvdbAlias[] Aliases { get; set; } = [];
    [JsonProperty("country")] public string Country { get; set; } = string.Empty;
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("inactiveDate")] public string InactiveDate { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("nameTranslations")] public string[] NameTranslations { get; set; } = [];
    [JsonProperty("overviewTranslations")] public string[] OverviewTranslations { get; set; } = [];
    [JsonProperty("primaryCompanyType")] public int PrimaryCompanyType { get; set; }
    [JsonProperty("slug")] public string Slug { get; set; } = string.Empty;
    [JsonProperty("parentCompany")] public TvdbParentCompany? ParentCompany { get; set; }
    [JsonProperty("tagOptions")] public TvdbCharacterTagOption[] TagOptions { get; set; } = [];
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
}

public class TvdbParentCompany
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("relation")] public TvdbRelation TvdbRelation { get; set; } = new();
}

public class TvdbRelation
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("typeName")] public string TypeName { get; set; } = string.Empty;
}


public class TvdbCompanyTypesResponse : TvdbResponse<TvdbCompanyType[]>
{
}

public class TvdbCompanyType
{
    [JsonProperty("companyTypeId")] public int CompanyTypeId { get; set; }
    [JsonProperty("companyTypeName")] public string CompanyTypeName { get; set; } = string.Empty;
}