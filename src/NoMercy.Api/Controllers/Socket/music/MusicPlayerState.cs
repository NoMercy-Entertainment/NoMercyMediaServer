using Newtonsoft.Json;
using NoMercy.Api.Controllers.Socket.shared;
using NoMercy.Api.Controllers.V1.Music.DTO;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicPlayerState
{
    [JsonProperty("actions")] public Actions Actions { get; set; } = new();
    [JsonProperty("device_id")] public string? DeviceId { get; set; }
    [JsonProperty("is_playing")] public bool PlayState { get; set; }
    [JsonProperty("item")] public PlaylistTrackDto? CurrentItem { get; set; }
    [JsonProperty("playlist")] public List<PlaylistTrackDto> Playlist { get; set; } = [];
    [JsonProperty("backlog")] public List<PlaylistTrackDto> Backlog { get; set; } = [];
    [JsonProperty("current_list")] public Uri CurrentList { get; set; } = null!;
    [JsonProperty("progress_ms")] public int Time { get; set; }
    [JsonProperty("duration_ms")] public int Duration { get; set; }
    [JsonProperty("repeat_state")] public string Repeat { get; set; } = "off";
    [JsonProperty("shuffle_state")] public bool Shuffle { get; set; }
    [JsonProperty("muted_state")] public bool Muted { get; set; }
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("volume_percentage")] public int VolumePercentage { get; set; }
    [JsonProperty("seek_offset")] public int SeekOffset { get; set; }
}