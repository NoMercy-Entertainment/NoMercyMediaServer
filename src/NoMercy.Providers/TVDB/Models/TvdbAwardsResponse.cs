using Newtonsoft.Json;
namespace NoMercy.Providers.TVDB.Models;
public class TvdbAwardsResponse: TvdbResponse<TvdbStatus[]>
{
}
public class TvdbAwardResponse : TvdbResponse<TvdbStatus>
{
}
public class TvdbAwardExtendedResponse : TvdbResponse<TvdbAwardExtendedData>
{
}
public class TvdbAwardExtendedData
{
    [JsonProperty("categories")] public List<TvdbAwardCategoryData> Categories { get; set; } = new();
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("score")] public int Score { get; set; }
}
public class TvdbAwardCategoryResponse : TvdbResponse<TvdbAwardCategoryData>
{
}
public class TvdbAwardCategoryData
{
    [JsonProperty("allowCoNominees")] public bool AllowCoNominees { get; set; }
    [JsonProperty("award")] public TvdbStatus Award { get; set; } = new();
    [JsonProperty("forMovies")] public bool ForMovies { get; set; }
    [JsonProperty("forSeries")] public bool ForSeries { get; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}
public class TvdbAwardCategoryExtendedResponse : TvdbResponse<TvdbAwardCategoryExtendedData>
{
}
public class TvdbAwardCategoryExtendedData : TvdbAwardCategoryData
{
    // [JsonProperty("id")] public int Id { get; set; }
    // [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    // [JsonProperty("allowCoNominees")] public bool AllowCoNominees { get; set; }
    // [JsonProperty("forSeries")] public bool ForSeries { get; set; }
    // [JsonProperty("forMovies")] public bool ForMovies { get; set; }
    // [JsonProperty("award")] public TvdbStatus Award { get; set; }
    // [JsonProperty("nominees")] public TvdbNominee[] Nominees { get; set; }
}

public class TvdbNominee
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("details")] public string? Details { get; set; }
    [JsonProperty("isWinner")] public bool IsWinner { get; set; }
    [JsonProperty("category")] public string? Category { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("series")] public string? Series { get; set; }
    [JsonProperty("movie")] public TvdbMovie? Movie { get; set; }
    [JsonProperty("episode")] public TvdbEpisode? Episode { get; set; }
    [JsonProperty("character")] public TvdbCharacter? Character { get; set; }
}

public class TvdbEpisode {}
public class TvdbCharacter {}
public class TvdbMovie
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("slug")] public string Slug { get; set; } = string.Empty;
    [JsonProperty("image")] public Uri? Image { get; set; }
    [JsonProperty("nameTranslations")] public string[] NameTranslations { get; set; } = [];
    [JsonProperty("overviewTranslations")] public string[] OverviewTranslations { get; set; } = [];
    [JsonProperty("aliases")] public TvdbAlias[] Aliases { get; set; } = [];
    [JsonProperty("score")] public int Score { get; set; }
    [JsonProperty("runtime")] public int Runtime { get; set; }
    [JsonProperty("status")] public TvdbAwardStatus? TvdbAwardStatus { get; set; }
    [JsonProperty("lastUpdated")] public DateTimeOffset LastUpdated { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
}
public class TvdbAlias
{ 
    [JsonProperty("language")] public string Language { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
}
public class TvdbAwardStatus
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("recordType")] public string RecordType { get; set; } = string.Empty;
    [JsonProperty("keepUpdated")] public bool KeepUpdated { get; set; }
}