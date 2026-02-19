using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.People;

public class TmdbPersonImages
{
    [JsonProperty("profiles")] public TmdbProfile[] Profiles { get; set; } = [];
}