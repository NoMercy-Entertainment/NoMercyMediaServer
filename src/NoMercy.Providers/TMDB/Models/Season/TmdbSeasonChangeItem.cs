using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Season;

public class TmdbSeasonChangeItem
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("action")] public Action? Action { get; set; }
    [JsonProperty("time")] public string Time { get; set; } = string.Empty;
    [JsonProperty("value")] public string? Value { get; set; }
    [JsonProperty("original_value")] public string OriginalValue { get; set; } = string.Empty;
    [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;
}