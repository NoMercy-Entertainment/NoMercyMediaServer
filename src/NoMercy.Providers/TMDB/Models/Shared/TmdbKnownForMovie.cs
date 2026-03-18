using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbKnownForMovie
{
    [JsonProperty("release_date")] public DateTime? ReleaseDate { get; set; }
}