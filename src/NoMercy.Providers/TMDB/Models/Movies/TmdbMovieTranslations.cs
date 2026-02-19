using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieTranslations : TmdbSharedTranslations
{
    [JsonProperty("translations")] public new TmdbMovieTranslation[] Translations { get; set; } = [];
}