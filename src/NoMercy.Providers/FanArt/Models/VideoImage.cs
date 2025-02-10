using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;
public class VideoImage : Image
{
    [JsonProperty("lang")] public string Language { get; set; } = string.Empty;
}