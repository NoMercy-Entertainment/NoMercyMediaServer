using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchUserBlobGet
{
    [JsonProperty("message")] public UserBlobGetMessage Message { get; set; } = new();
    [JsonProperty("meta")] public UserBlobGetMeta Meta { get; set; } = new();
}
