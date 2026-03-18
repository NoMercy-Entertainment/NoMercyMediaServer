using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Certifications;

public class TvShowCertifications
{
    [JsonProperty("certifications")]
    public Dictionary<string, TmdbTvShowCertification[]> Certifications { get; set; } = new();
}