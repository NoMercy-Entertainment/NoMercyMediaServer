using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchTrackRichSyncGet
{
    [JsonProperty("richsync")] public MusixMatchRichSync MusixMatchRichSync { get; set; } = new();
}
