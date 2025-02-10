using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;


namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonTaggedImages : TmdbPaginatedResponse<TmdbTaggedImage>
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
}
