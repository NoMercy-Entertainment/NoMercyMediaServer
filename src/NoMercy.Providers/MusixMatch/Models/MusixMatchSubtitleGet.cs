using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchSubtitleGet
{
    [JsonProperty("message")] public MusixMatchSubtitleGetMessage? Message { get; set; }
}