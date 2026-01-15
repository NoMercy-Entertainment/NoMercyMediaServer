using Newtonsoft.Json;

namespace NoMercy.MediaSources.OpticalMedia.Dto;

public class MediaProcessingRequest
{
    [JsonProperty("playlists")] public OpticalPlaylist[] Playlists { get; set; } = [];
}

public class OpticalPlaylist
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;

    [JsonProperty("video_tracks")] public int[] VideoTracks { get; set; } = [];
    [JsonProperty("audio_tracks")] public int[] AudioTracks { get; set; } = [];
    [JsonProperty("subtitle_tracks")] public int[] SubtitleTracks { get; set; } = [];
}