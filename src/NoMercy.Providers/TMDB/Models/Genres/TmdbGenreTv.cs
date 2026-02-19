using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Providers.TMDB.Models.Genres;

public class TmdbGenreTv
{
    [JsonProperty("genres")] public TmdbGenre[] Genres { get; set; } = [];
}