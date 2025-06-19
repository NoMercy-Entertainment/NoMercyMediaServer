using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Credits;

public class TmdbPerson
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("id")] public int Id { get; set; }
}