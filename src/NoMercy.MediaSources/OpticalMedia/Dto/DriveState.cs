using Newtonsoft.Json;

namespace NoMercy.MediaSources.OpticalMedia.Dto;

public class DriveState
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;

    [JsonProperty("label")] public string? Label { get; set; }

    // [JsonProperty("driveType")] public string DriveType { get; set; } = string.Empty;
    [JsonProperty("open")] public bool Open { get; set; }
    [JsonProperty("metadata")] public MetaData? MetaData { get; set; }
}