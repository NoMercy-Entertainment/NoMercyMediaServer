using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieTranslation : TmdbSharedTranslation
{
    [JsonProperty("data")] public new TmdbMovieTranslationData Data { get; set; } = new();
}