using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Movies;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Awards;

public class TvdbAwardsResponse : TvdbResponse<TvdbAward[]> { }

public class TvdbAwardResponse : TvdbResponse<TvdbAward> { }

public class TvdbAwardExtendedResponse : TvdbResponse<TvdbAwardExtended> { }

public class TvdbAwardCategoryResponse : TvdbResponse<TvdbAwardCategory> { }

public class TvdbAwardCategoryExtendedResponse : TvdbResponse<TvdbAwardCategoryExtended> { }

public class TvdbAward
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

public class TvdbAwardExtended
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("score")]
    public int Score { get; set; }

    [JsonProperty("categories")]
    public List<TvdbAwardCategory> Categories { get; set; } = [];
}

public class TvdbAwardCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("allowCoNominees")]
    public bool AllowCoNominees { get; set; }

    [JsonProperty("award")]
    public TvdbAward Award { get; set; } = new();

    [JsonProperty("forMovies")]
    public bool ForMovies { get; set; }

    [JsonProperty("forSeries")]
    public bool ForSeries { get; set; }
}

public class TvdbAwardCategoryExtended : TvdbAwardCategory
{
    [JsonProperty("nominees")]
    public List<TvdbNominee> Nominees { get; set; } = [];
}

public class TvdbNominee
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("year")]
    public int Year { get; set; }

    [JsonProperty("details")]
    public string? Details { get; set; }

    [JsonProperty("isWinner")]
    public bool IsWinner { get; set; }

    [JsonProperty("category")]
    public string? Category { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("series")]
    public string? Series { get; set; }

    [JsonProperty("movie")]
    public TvdbMovie? Movie { get; set; }
}
