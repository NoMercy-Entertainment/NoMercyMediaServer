using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbSharedKeywords
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("results")] public virtual TmdbKeyword[] Results { get; set; } = [];
}