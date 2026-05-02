using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Genres;

public class TvdbGenresResponse : TvdbResponse<TvdbGenre[]> { }

public class TvdbGenreResponse : TvdbResponse<TvdbGenre> { }

public class TvdbGenre
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;
}
