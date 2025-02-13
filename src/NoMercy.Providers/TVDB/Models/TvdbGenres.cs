using Newtonsoft.Json;

namespace NoMercy.Providers.TVDB.Models;

public class TvdbGenresResponse: TvdbResponse<TvdbGenre[]>
{
}
public class TvdbGenreResponse: TvdbResponse<TvdbGenre>
{
}
public class TvdbGenre
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("slug")] public string Slug { get; set; } = string.Empty;
}