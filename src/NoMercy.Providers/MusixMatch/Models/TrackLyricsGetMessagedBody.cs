using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class TrackLyricsGetMessagedBody
{
    [JsonProperty("lyrics")] public MusixMatchLyrics? Lyrics { get; set; }
}