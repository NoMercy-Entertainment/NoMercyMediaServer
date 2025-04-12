using MovieFileLibrary;
using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;
public record FileItemDto
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("mode")] public int Mode { get; set; }
    [JsonProperty("parent")] public string? Parent { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("parsed")] public MovieFile? Parsed { get; set; }
    [JsonProperty("match")] public MovieOrEpisodeDto Match { get; set; } = new();
    [JsonProperty("streams")] public StreamsDto Streams { get; set; } = new();
    [JsonProperty("file")] public string File { get; set; } = string.Empty;
    [JsonProperty("tracks")] public int Tracks { get; set; }
}