using Newtonsoft.Json;
using NoMercy.Api.Hubs.Shared;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.video;

public class VideoPlayerState
{
    [JsonProperty("actions")] public Actions Actions { get; set; } = null!;
    [JsonProperty("device_id")] public string? DeviceId { get; set; }
    [JsonProperty("is_playing")] public bool PlayState { get; set; }
    [JsonProperty("item")] public VideoPlaylistResponseDto? CurrentItem { get; set; }
    [JsonProperty("playlist")] public List<VideoPlaylistResponseDto> Playlist { get; set; } = [];
    [JsonProperty("progress_ms")] public int Time { get; set; }
    [JsonProperty("duration_ms")] public int Duration { get; set; }
    [JsonProperty("current_list")] public Uri CurrentList { get; set; } = null!;
    [JsonProperty("muted_state")] public bool Muted { get; set; }
    [JsonProperty("timestamp")] public long Timestamp { get; set; }
    [JsonProperty("volume_percentage")] public int VolumePercentage { get; set; }
    [JsonProperty("seek_offset")] public int SeekOffset { get; set; }
    
    [JsonProperty("current_caption")] public ISubtitle? CurrentCaption { get; set; }
    [JsonProperty("current_audio")] public IAudio? CurrentAudio { get; set; }
    [JsonProperty("current_quality")] public IVideo? CurrentQuality { get; set; }
}
