using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbCharacterResponse: TvdbResponse<TvdbCharacterData>
{
}
public class TvdbCharacterData
{
    [JsonProperty("aliases")] public List<TvdbAlias> Aliases { get; set; } = [];
    [JsonProperty("episodeId")] public int? EpisodeId { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("image")] public Uri? Image { get; set; }
    [JsonProperty("isFeatured")] public bool IsFeatured { get; set; }
    [JsonProperty("movieId")] public int? MovieId { get; set; }
    [JsonProperty("movie")] public TvdbInfo? Movie { get; set; } = new();
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("nameTranslations")] public string[] NameTranslations { get; set; } = [];
    [JsonProperty("overviewTranslations")] public string[] OverviewTranslations { get; set; } = [];
    [JsonProperty("peopleId")] public int PeopleId { get; set; }
    [JsonProperty("personImgURL")] public Uri? PersonImgUrl { get; set; }
    [JsonProperty("peopleType")] public string PeopleType { get; set; } = string.Empty;
    [JsonProperty("seriesId")] public int? SeriesId { get; set; }
    [JsonProperty("series")] public TvdbInfo? Series { get; set; } = new();
    [JsonProperty("sort")] public int Sort { get; set; }
    [JsonProperty("tagOptions")] public List<TvdbCharacterTagOption> TagOptions { get; set; } = [];
    [JsonProperty("type")] public int Type { get; set; }
    [JsonProperty("url")] public required Uri Url { get; set; }
    [JsonProperty("personName")] public string PersonName { get; set; } = string.Empty;
}

public class TvdbInfo
{
    [JsonProperty("image")] public Uri? Image { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("year")] public string? Year { get; set; }
}

public class TvdbCharacterTagOption
{
    [JsonProperty("helpText")] public string? HelpText { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("tag")] public int Tag { get; }
    [JsonProperty("tagName")] public string? TagName { get; set; }
}