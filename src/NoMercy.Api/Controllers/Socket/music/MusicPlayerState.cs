using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Music.DTO;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicPlayerState
{
    [JsonProperty("actions")] public Actions? Actions { get; set; }
    [JsonProperty("device_id")] public string? DeviceId { get; set; }
    [JsonProperty("is_playing")] public bool PlayState { get; set; }
    [JsonProperty("item")] public PlaylistTrackDto? CurrentItem { get; set; } = null!;
    [JsonProperty("playlist")] public List<PlaylistTrackDto> Playlist { get; set; } = [];
    [JsonProperty("backlog")] public List<PlaylistTrackDto> Backlog { get; set; } = [];
    [JsonProperty("progress_ms")] public int Time { get; set; }
    [JsonProperty("duration_ms")] public int Duration { get; set; }
    [JsonProperty("repeat_state")] public string Repeat { get; set; } = "off";
    [JsonProperty("current_list")] public string CurrentList { get; set; } = null!;
    [JsonProperty("shuffle_state")] public bool Shuffle { get; set; }
    [JsonProperty("muted_state")] public bool Muted { get; set; }
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("volume_percentage")] public int VolumePercentage { get; set; }
    [JsonProperty("seek_offset")] public int SeekOffset { get; set; }
}

public class Actions
{
    [JsonProperty("disallows")] public Disallows? Disallows { get; set; } = new();
}

public class Disallows
{
    [JsonProperty("previous", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Previous { get; set; }

    [JsonProperty("resuming", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Resuming { get; set; }

    [JsonProperty("pausing", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Pausing { get; set; }

    [JsonProperty("toggling_repeat_context", NullValueHandling = NullValueHandling.Ignore)]
    public bool? TogglingRepeatContext { get; set; }

    [JsonProperty("toggling_repeat_track", NullValueHandling = NullValueHandling.Ignore)]
    public bool? TogglingRepeatTrack { get; set; }

    [JsonProperty("toggling_shuffle", NullValueHandling = NullValueHandling.Ignore)]
    public bool? TogglingShuffle { get; set; }
}