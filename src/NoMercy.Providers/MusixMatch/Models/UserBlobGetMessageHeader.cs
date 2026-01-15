using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class UserBlobGetMessageHeader
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
}