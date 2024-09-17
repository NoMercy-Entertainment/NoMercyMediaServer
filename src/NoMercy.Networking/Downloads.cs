using Newtonsoft.Json;

namespace NoMercy.Networking;
public class Downloads
{
    [JsonProperty("windows")] public List<Download> Windows { get; set; } = [];
    [JsonProperty("linux")] public List<Download> Linux { get; set; } = [];
    [JsonProperty("mac")] public List<Download> Mac { get; set; } = [];
}