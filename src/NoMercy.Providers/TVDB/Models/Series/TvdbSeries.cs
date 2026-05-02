using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Artwork;
using NoMercy.Providers.TVDB.Models.Awards;
using NoMercy.Providers.TVDB.Models.Characters;
using NoMercy.Providers.TVDB.Models.Companies;
using NoMercy.Providers.TVDB.Models.ContentRatings;
using NoMercy.Providers.TVDB.Models.Episodes;
using NoMercy.Providers.TVDB.Models.Genres;
using NoMercy.Providers.TVDB.Models.Lists;
using NoMercy.Providers.TVDB.Models.Seasons;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Series;

public class TvdbSeriesResponse : TvdbResponse<TvdbSeries> { }

public class TvdbSeriesExtendedResponse : TvdbResponse<TvdbSeriesExtended> { }

public class TvdbSeriesEpisodesResponse : TvdbResponse<TvdbSeriesEpisodes> { }

public class TvdbSeriesStatusesResponse : TvdbResponse<TvdbStatus[]> { }

public class TvdbSeriesTranslationResponse : TvdbResponse<TvdbTranslationData> { }

public class TvdbNextAiredResponse : TvdbResponse<TvdbSeries> { }

public class TvdbSeries
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonProperty("image")]
    public Uri? Image { get; set; }

    [JsonProperty("abbreviation")]
    public string? Abbreviation { get; set; }

    [JsonProperty("country")]
    public string? Country { get; set; }

    [JsonProperty("defaultSeasonType")]
    public int DefaultSeasonType { get; set; }

    [JsonProperty("episodes")]
    public TvdbEpisode[]? Episodes { get; set; }

    [JsonProperty("firstAired")]
    public string? FirstAired { get; set; }

    [JsonProperty("lastAired")]
    public string? LastAired { get; set; }

    [JsonProperty("nextAired")]
    public string? NextAired { get; set; }

    [JsonProperty("originalCountry")]
    public string? OriginalCountry { get; set; }

    [JsonProperty("originalLanguage")]
    public string? OriginalLanguage { get; set; }

    [JsonProperty("originalNetwork")]
    public TvdbCompany? OriginalNetwork { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("score")]
    public double Score { get; set; }

    [JsonProperty("status")]
    public TvdbStatus? Status { get; set; }

    [JsonProperty("year")]
    public string? Year { get; set; }

    [JsonProperty("nameTranslations")]
    public string[] NameTranslations { get; set; } = [];

    [JsonProperty("overviewTranslations")]
    public string[] OverviewTranslations { get; set; } = [];

    [JsonProperty("aliases")]
    public TvdbAlias[] Aliases { get; set; } = [];

    [JsonProperty("averageRuntime")]
    public int? AverageRuntime { get; set; }

    [JsonProperty("isOrderRandomized")]
    public bool IsOrderRandomized { get; set; }

    [JsonProperty("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }
}

public class TvdbSeriesExtended : TvdbSeries
{
    [JsonProperty("artworks")]
    public TvdbArtwork[]? Artworks { get; set; }

    [JsonProperty("airsDays")]
    public TvdbAirsDays? AirsDays { get; set; }

    [JsonProperty("airsTime")]
    public string? AirsTime { get; set; }

    [JsonProperty("awards")]
    public TvdbAward[]? Awards { get; set; }

    [JsonProperty("characters")]
    public TvdbCharacter[]? Characters { get; set; }

    [JsonProperty("companies")]
    public TvdbCompany[]? Companies { get; set; }

    [JsonProperty("contentRatings")]
    public TvdbContentRating[]? ContentRatings { get; set; }

    [JsonProperty("genres")]
    public TvdbGenre[]? Genres { get; set; }

    [JsonProperty("latestNetwork")]
    public TvdbCompany? LatestNetwork { get; set; }

    [JsonProperty("lists")]
    public TvdbList[]? Lists { get; set; }

    [JsonProperty("networks")]
    public TvdbCompany[]? Networks { get; set; }

    [JsonProperty("remoteIds")]
    public TvdbRemoteId[]? RemoteIds { get; set; }

    [JsonProperty("seasons")]
    public TvdbSeason[]? Seasons { get; set; }

    [JsonProperty("seasonTypes")]
    public TvdbSeasonType[]? SeasonTypes { get; set; }

    [JsonProperty("studios")]
    public TvdbCompany[]? Studios { get; set; }

    [JsonProperty("tags")]
    public TvdbTagOption[]? Tags { get; set; }

    [JsonProperty("trailers")]
    public TvdbTrailer[]? Trailers { get; set; }

    [JsonProperty("translations")]
    public TvdbTranslations? Translations { get; set; }
}

public class TvdbAirsDays
{
    [JsonProperty("monday")]
    public bool Monday { get; set; }

    [JsonProperty("tuesday")]
    public bool Tuesday { get; set; }

    [JsonProperty("wednesday")]
    public bool Wednesday { get; set; }

    [JsonProperty("thursday")]
    public bool Thursday { get; set; }

    [JsonProperty("friday")]
    public bool Friday { get; set; }

    [JsonProperty("saturday")]
    public bool Saturday { get; set; }

    [JsonProperty("sunday")]
    public bool Sunday { get; set; }
}

public class TvdbSeriesEpisodes
{
    [JsonProperty("series")]
    public TvdbSeries? Series { get; set; }

    [JsonProperty("episodes")]
    public TvdbEpisode[] Episodes { get; set; } = [];
}
