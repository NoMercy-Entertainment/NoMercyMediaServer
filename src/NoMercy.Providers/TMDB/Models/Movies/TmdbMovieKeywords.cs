using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieKeywords : TmdbSharedKeywords
{
    [JsonProperty("keywords")] public override TmdbKeyword[] Results { get; set; } = [];
}