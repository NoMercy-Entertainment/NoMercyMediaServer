using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.WatchProviders;

public class TmdbMovieProviders
{
    [JsonProperty("results")] public TmdbProvider[] Results { get; set; } = [];
}