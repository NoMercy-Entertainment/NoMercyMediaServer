using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieChanges
{
    [JsonProperty("changes")] public TmdbChanges[] ChangesChanges { get; set; } = [];
}