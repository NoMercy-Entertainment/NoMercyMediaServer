using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class UserBlobGetMeta
{
    [JsonProperty("status_code")] public long StatusCode { get; set; }
    [JsonProperty("last_updated")] public DateTimeOffset LastUpdated { get; set; }
}