using Newtonsoft.Json;


namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbLogo
{
    [JsonProperty("aspect_ratio")] public double AspectRatio { get; set; }
    [JsonProperty("file_path")] public string FilePath { get; set; } = string.Empty;
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("file_type")] public string FileType { get; set; } = string.Empty;
    [JsonProperty("vote_average")] public int VoteAverage { get; set; }
    [JsonProperty("vote_count")] public int VoteCount { get; set; }
    [JsonProperty("width")] public int Width { get; set; }
}
