using MovieFileLibrary;
using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.MediaProcessing.Files;

public class FileItem
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("mode")] public int Mode { get; set; }
    [JsonProperty("parent")] public string? Parent { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("parsed")] public MovieFile? Parsed { get; set; }
    [JsonProperty("match")] public MovieOrEpisode Match { get; set; } = new();
    [JsonProperty("streams")] public Streams Streams { get; set; } = new();
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("tracks")] public int Tracks { get; set; }
}