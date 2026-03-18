using Newtonsoft.Json;

namespace NoMercy.Providers.TMDB.Models.TV;

public class TmdbAuthorDetails
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("username")] public string Username { get; set; } = string.Empty;
    [JsonProperty("avatar_path")] public string AvatarPath { get; set; } = string.Empty;
    [JsonProperty("rating")] public int Rating { get; set; }
}