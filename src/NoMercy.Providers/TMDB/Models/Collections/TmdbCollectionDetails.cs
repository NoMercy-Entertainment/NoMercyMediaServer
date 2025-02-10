using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Movies;


namespace NoMercy.Providers.TMDB.Models.Collections;

public class TmdbCollectionDetails : TmdbCollection
{
    [JsonProperty("overview")] public string Overview { get; set; } = string.Empty;
    [JsonProperty("parts")] public TmdbMovie[] Parts { get; set; } = [];
}
