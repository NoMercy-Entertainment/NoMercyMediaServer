using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models.Shared;

public class TvdbStatus
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("recordType")]
    public string? RecordType { get; set; }

    [JsonProperty("keepUpdated")]
    public bool KeepUpdated { get; set; }
}

public class TvdbRemoteId
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type")]
    public int Type { get; set; }

    [JsonProperty("sourceName")]
    public string SourceName { get; set; } = string.Empty;
}

public class TvdbTagOption
{
    [JsonProperty("helpText")]
    public string? HelpText { get; set; }

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("tag")]
    public int Tag { get; set; }

    [JsonProperty("tagName")]
    public string? TagName { get; set; }
}

public class TvdbTrailer
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("runtime")]
    public int? Runtime { get; set; }
}

public class TvdbTranslationData
{
    [JsonProperty("aliases")]
    public string[]? Aliases { get; set; }

    [JsonProperty("isAlias")]
    public bool? IsAlias { get; set; }

    [JsonProperty("isPrimary")]
    public bool? IsPrimary { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("tagline")]
    public string? Tagline { get; set; }
}

public class TvdbTranslations
{
    [JsonProperty("nameTranslations")]
    public TvdbTranslationData[]? NameTranslations { get; set; }

    [JsonProperty("overviewTranslations")]
    public TvdbTranslationData[]? OverviewTranslations { get; set; }

    [JsonProperty("alias")]
    public string[]? Alias { get; set; }
}
