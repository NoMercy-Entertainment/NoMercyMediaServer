using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Certifications;

public class TmdbMovieCertifications
{
    [JsonProperty("certifications")]
    public Dictionary<string, TmdbMovieCertification[]> Certifications { get; set; } = new();
}
