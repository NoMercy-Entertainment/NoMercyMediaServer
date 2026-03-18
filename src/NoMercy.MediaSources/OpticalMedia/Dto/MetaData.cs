using Newtonsoft.Json;

namespace NoMercy.MediaSources.OpticalMedia.Dto;

public class MetaData
{
    [JsonProperty("title")] public string Title = string.Empty;
    [JsonProperty("playlists")] public IEnumerable<PlaylistItem>? Playlists;
    [JsonProperty("data")] public dynamic? Data { get; set; }
    [JsonProperty("bluRay_playlists")] public List<BluRayPlaylist> BluRayPlaylists { get; set; } = [];
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
}

public class PlaylistItem
{
    [JsonProperty("playlist")] public string Playlist = string.Empty;
    [JsonProperty("streams")] public IEnumerable<Stream>? Streams;
    [JsonProperty("duration")] public string Duration = string.Empty;
    [JsonProperty("chapters")] public IEnumerable<ChapterData> Chapters { get; set; } = [];
}

public class ChapterData(string title, TimeSpan start, TimeSpan end)
{
    public string Title { get; set; } = title;
    public TimeSpan Start { get; } = start;
    public TimeSpan End { get; } = end;
    public TimeSpan Duration => End - Start;
}