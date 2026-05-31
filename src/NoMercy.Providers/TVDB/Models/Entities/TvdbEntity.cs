using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Entities;

public class TvdbEntitiesResponse : TvdbResponse<TvdbEntity[]> { }

public class TvdbEntity
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("hasSpecials")]
    public bool HasSpecials { get; set; }
}
