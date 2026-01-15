using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieTranslationData : TmdbSharedTranslationData
{
    [JsonProperty("title")] public string? Title { get; set; }
}