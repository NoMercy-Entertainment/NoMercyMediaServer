using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonChangeItem
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("action")] public string Action { get; set; } = string.Empty;
    [JsonProperty("time")] public string Time { get; set; } = string.Empty;

    [JsonProperty("original_value")]
    public TmdbPersonChangeOriginalValue TmdbPersonChangeOriginalValue { get; set; } = new();
}