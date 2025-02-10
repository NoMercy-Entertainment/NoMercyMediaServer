using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchLyricsUser
{
    [JsonProperty("user")] public MusixMatchUser MusixMatchUser { get; set; } = new();
}
