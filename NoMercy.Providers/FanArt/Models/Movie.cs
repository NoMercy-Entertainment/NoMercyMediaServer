using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class Movie
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("tmdb_id")] public int TmdbId { get; set; }
    [JsonProperty("imdb_id")] public string ImdbId { get; set; }
    [JsonProperty("hdmovielogo")] public VideoImage HdLogo { get; set; }
    [JsonProperty("movieposter")] public VideoImage Poster { get; set; }
    [JsonProperty("moviedisc")] public VideoImage Disc { get; set; }
    [JsonProperty("movielogo")] public VideoImage Logo { get; set; }
    [JsonProperty("moviethumb")] public VideoImage Thumb { get; set; }
    [JsonProperty("moviebanner")] public VideoImage Banner { get; set; }
}