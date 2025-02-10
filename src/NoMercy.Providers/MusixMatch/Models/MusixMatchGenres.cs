using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchGenres
{
    [JsonProperty("music_genre_list")] public MusixMatchMusicGenreList[] MusicGenreList { get; set; } = [];
}
