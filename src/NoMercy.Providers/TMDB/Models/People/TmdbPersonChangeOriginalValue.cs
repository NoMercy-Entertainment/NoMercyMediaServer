using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonChangeOriginalValue
{
    [JsonProperty("profile")] public string Profile { get; set; } = string.Empty;
}