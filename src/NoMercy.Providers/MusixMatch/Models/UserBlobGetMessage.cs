using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class UserBlobGetMessage
{
    [JsonProperty("header")] public UserBlobGetMessageHeader Header { get; set; } = new();
}