using Newtonsoft.Json;

namespace NoMercy.Encoder.Core;
public class ProgressMeta
{
    [JsonProperty("has_gpu")] public bool HasGpu { get; set; }
    [JsonProperty("is_hdr")] public bool IsHdr { get; set; }
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("base_folder")] public string BaseFolder { get; set; } = string.Empty;
    [JsonProperty("share_path")] public string ShareBasePath { get; set; } = string.Empty;
    [JsonProperty("video_streams")] public List<string> VideoStreams { get; set; } = [];
    [JsonProperty("audio_streams")] public List<string> AudioStreams { get; set; } = [];
    [JsonProperty("subtitle_streams")] public List<string> SubtitleStreams { get; set; } = [];
}