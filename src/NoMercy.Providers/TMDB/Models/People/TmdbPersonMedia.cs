using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonMedia
{
    [JsonProperty("_id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("id")] public int MediaId { get; set; }
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
}