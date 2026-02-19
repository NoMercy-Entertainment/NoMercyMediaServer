using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Networks;

public class TmdbNetworkImages
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("logos")] public TmdbLogo[] Logos { get; set; } = [];
}