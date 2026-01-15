using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchSubtitleGetMessageBody
{
    [JsonProperty("macro_calls")] public MusixMatchMacroCalls? MacroCalls { get; set; }
}