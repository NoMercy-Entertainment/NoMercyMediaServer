using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record Profile
{
    [JsonProperty("aspect_ratio")] public double AspectRatio { get; set; }
    [JsonProperty("height")] public long Height { get; set; }
    [JsonProperty("iso_639_1")] public object Iso6391 { get; set; } = string.Empty;
    [JsonProperty("file_path")] public string FilePath { get; set; } = string.Empty;
    [JsonProperty("vote_average")] public double VoteAverage { get; set; }
    [JsonProperty("vote_count")] public long VoteCount { get; set; }
    [JsonProperty("width")] public long Width { get; set; }
}