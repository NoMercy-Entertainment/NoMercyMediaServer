using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbChangeListItem
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("adult")]
    public bool? Adult { get; set; }
}
