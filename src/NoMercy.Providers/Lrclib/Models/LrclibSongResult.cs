using Newtonsoft.Json;

namespace NoMercy.Providers.Lrclib.Models;
            
[Serializable]
public class LrclibSongResult
{
    [JsonProperty("id")]
    public int Id { get; set; } = 0;
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    [JsonProperty("trackName")]
    public string TrackName { get; set; } = string.Empty;
    [JsonProperty("artistName")]
    public string ArtistName { get; set; } = string.Empty;
    [JsonProperty("albumName")]
    public string AlbumName { get; set; } = string.Empty;
    [JsonProperty("duration")]
    public double Duration { get; set; } = 0.0;
    [JsonProperty("instrumental")]
    public bool Instrumental { get; set; } = false;
    [JsonProperty("plainLyrics")]
    public string PlainLyrics { get; set; } = string.Empty;
    [JsonProperty("syncedLyrics")]
    public string SyncedLyrics { get; set; } = string.Empty;
        
    // Error handling
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
    [JsonProperty("statusCode")]
    public int StatusCode { get; set; } = 200;
}