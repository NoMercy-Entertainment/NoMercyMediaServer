using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record ParamsDto
{
    [JsonProperty("video")] public int Width { get; set; }
    [JsonProperty("crf")] public int Crf { get; set; }
    [JsonProperty("preset")] public string Preset { get; set; } = string.Empty;
    [JsonProperty("profile")] public string Profile { get; set; } = string.Empty;
    [JsonProperty("codec")] public string Codec { get; set; } = string.Empty;
    [JsonProperty("audio")] public string Audio { get; set; } = string.Empty;
    [JsonProperty("tune")] public string Tune { get; set; } = string.Empty;
    [JsonProperty("level")] public string Level { get; set; } = string.Empty;
}