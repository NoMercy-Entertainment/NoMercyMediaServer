using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchMusicGenreList
{
    [JsonProperty("music_genre")] public MusixMatchMusicGenre MusixMatchMusicGenre { get; set; } = new();
}