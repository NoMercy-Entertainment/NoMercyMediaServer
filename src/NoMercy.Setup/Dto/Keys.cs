using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class Keys
{
    [JsonProperty("makemkv_key")] public string MakeMkvKey { get; set; } = string.Empty;
    [JsonProperty("tmdb_key")] public string TmdbKey { get; set; } = string.Empty;
    [JsonProperty("omdb_key")] public string OmdbKey { get; set; } = string.Empty;
    [JsonProperty("fanart_key")] public string FanArtKey { get; set; } = string.Empty;
    [JsonProperty("rotten_tomatoes")] public string RottenTomatoes { get; set; } = string.Empty;
    [JsonProperty("acoustic_id")] public string AcousticId { get; set; } = string.Empty;
    [JsonProperty("tadb_key")] public string TadbKey { get; set; } = string.Empty;
    [JsonProperty("tmdb_token")] public string TmdbToken { get; set; } = string.Empty;
    [JsonProperty("tvdb_key")] public string TvdbKey { get; set; } = string.Empty;
    [JsonProperty("musixmatch_key")] public string MusixmatchKey { get; set; } = string.Empty;
    [JsonProperty("jwplayer_key")] public string JwplayerKey { get; set; } = string.Empty;
}