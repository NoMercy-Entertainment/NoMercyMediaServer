using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.Configuration;

public class TmdbConfiguration
{
    [JsonProperty("images")] public TmdbImage TmdbImages { get; set; } = new();
    [JsonProperty("change_keys")] public string[] ChangeKeys { get; set; } = [];

    public class TmdbImage
    {
        [JsonProperty("base_url")] public string BaseUrl { get; set; } = string.Empty;
        [JsonProperty("secure_base_url")] public string SecureBaseUrl { get; set; } = string.Empty;
        [JsonProperty("backdrop_sizes")] public string[] BackdropSizes { get; set; } = [];
        [JsonProperty("logo_sizes")] public string[] LogoSizes { get; set; } = [];
        [JsonProperty("poster_sizes")] public string[] PosterSizes { get; set; } = [];
        [JsonProperty("profile_sizes")] public string[] ProfileSizes { get; set; } = [];
        [JsonProperty("still_sizes")] public string[] StillSizes { get; set; } = [];
    }
}