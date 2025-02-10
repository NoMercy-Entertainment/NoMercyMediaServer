using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Movies;

public class TmdbMovieCertifications
{
    [JsonProperty("results")] public MovieCertification[] Results { get; set; } = [];
}
