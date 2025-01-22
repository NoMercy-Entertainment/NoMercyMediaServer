using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Networking;
using NoMercy.NmSystem;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Api.Controllers.Socket;

public class CastHub : ConnectionHub
{
    public CastHub(IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
    {
    }

    public class TimeData
    {
        [JsonProperty("currentTime")] public double CurrentTime { get; set; }
        [JsonProperty("duration")] public double Duration { get; set; }
        [JsonProperty("percentage")] public double Percentage { get; set; }
        [JsonProperty("remaining")] public double Remaining { get; set; }
        [JsonProperty("currentTimeHuman")] public string CurrentTimeHuman { get; set; } = string.Empty;
        [JsonProperty("durationHuman")] public string DurationHuman { get; set; } = string.Empty;
        [JsonProperty("remainingHuman")] public string RemainingHuman { get; set; } = string.Empty;
    }

    public class TextTrack
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("default")] public bool Default { get; set; }
        [JsonProperty("file")] public string File { get; set; } = string.Empty;
        [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;
        [JsonProperty("label")] public string? Label { get; set; }
        [JsonProperty("language")] public string? Language { get; set; }
        [JsonProperty("type")] public string? Type { get; set; }
        [JsonProperty("ext")] public string? Ext { get; set; }
    }

    public class AudioTrack
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("language")] public string Language { get; set; } = string.Empty;
        [JsonProperty("label")] public string Label { get; set; } = string.Empty;
    }

    public class PlaylistItem
    {
        [JsonProperty("id")] public string Id { get; set; } = string.Empty;
        [JsonProperty("uuid")] public string Uuid { get; set; } = string.Empty;
        [JsonProperty("seasonName")] public string SeasonName { get; set; } = string.Empty;
        [JsonProperty("progress")] public ProgressDto Progress { get; set; } = new();
        [JsonProperty("duration")] public string Duration { get; set; } = string.Empty;
        [JsonProperty("file")] public string File { get; set; } = string.Empty;
        [JsonProperty("image")] public string Image { get; set; } = string.Empty;
        [JsonProperty("title")] public string Title { get; set; } = string.Empty;
        [JsonProperty("tracks")] public TextTrack[] Tracks { get; set; } = [];
        [JsonProperty("withCredentials")] public bool WithCredentials { get; set; }
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("season")] public int Season { get; set; }
        [JsonProperty("episode")] public int Episode { get; set; }
        [JsonProperty("show")] public string Show { get; set; } = string.Empty;
        [JsonProperty("year")] public int Year { get; set; }
        [JsonProperty("logo")] public string Logo { get; set; } = string.Empty;
        [JsonProperty("rating")] public RatingDto Rating { get; set; } = new();
    }

    public class CastPlayerState
    {
        [JsonProperty("time")] public TimeData TimeData { get; set; } = new();
        [JsonProperty("volume")] public int Volume { get; set; }
        [JsonProperty("muted")] public bool Muted { get; set; }
        [JsonProperty("isPlaying")] public bool IsPlaying { get; set; }
        [JsonProperty("playlist")] public PlaylistItem[] Playlist { get; set; } = [];
        [JsonProperty("currentPlaylistItem")] public PlaylistItem? CurrentPlaylistItem { get; set; }
        [JsonProperty("subtitles")] public TextTrack[] Subtitles { get; set; } = [];
        [JsonProperty("currentSubtitleTrack")] public TextTrack CurrentSubtitleTextTrack { get; set; } = new();
        [JsonProperty("audioTracks")] public AudioTrack[] AudioTracks { get; set; } = [];
        [JsonProperty("currentAudioTrack")] public int CurrentAudioTrack { get; set; }
    }


    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Logger.Socket("Cast client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        Logger.Socket("Cast client disconnected");
    }

    public string[] GetChromeCasts()
    {
        return ChromeCast.GetChromeCasts();
    }

    public async Task SelectChromecast(string name)
    {
        await ChromeCast.SelectChromecast(name);
    }

    public async Task Launch()
    {
        await ChromeCast.Launch();
    }

    public async Task CastPlaylist(string value)
    {
        await ChromeCast.CastPlaylist(value);
    }

    public ChromecastStatus? GetChromecastStatus()
    {
        return ChromeCast.GetChromecastStatus();
    }

    public MediaStatus? GetMediaStatus()
    {
        return ChromeCast.GetMediaStatus();
    }

    public async Task Stop()
    {
        await ChromeCast.Stop();
    }

    public async Task Disconnect()
    {
        await ChromeCast.Disconnect();
    }

    public void Play()
    {
        Networking.Networking.SendToAll("Play", "castHub");
    }

    public void Pause()
    {
        Networking.Networking.SendToAll("Pause", "castHub");
    }

    public void Time(TimeData time)
    {
        Networking.Networking.SendToAll("Time", "castHub", time);
    }

    public void Ended()
    {
        Networking.Networking.SendToAll("Ended", "castHub");
    }

    public void Volume(int volume)
    {
        Networking.Networking.SendToAll("Volume", "castHub", volume);
    }

    public void Muted(bool muted)
    {
        Networking.Networking.SendToAll("Muted", "castHub", muted);
    }

    public void Item(PlaylistItem item)
    {
        Networking.Networking.SendToAll("Item", "castHub", item);
    }

    public void Playlist(PlaylistItem[] item)
    {
        Networking.Networking.SendToAll("Playlist", "castHub", item);
    }

    public void SubtitleTracks(TextTrack[] subtitleTracks)
    {
        Networking.Networking.SendToAll("SubtitleTracks", "castHub", subtitleTracks);
    }

    public void CurrentSubtitleTrack(TextTrack subtitleTrack)
    {
        Networking.Networking.SendToAll("CurrentSubtitleTrack", "castHub", subtitleTrack);
    }

    public void AudioTracks(AudioTrack[] audioTrack)
    {
        Networking.Networking.SendToAll("AudioTracks", "castHub", audioTrack);
    }

    public void CurrentAudioTrack(AudioTrack audioTrack)
    {
        Networking.Networking.SendToAll("CurrentAudioTrack", "castHub", audioTrack);
    }

    public void GetPlayerState()
    {
        Networking.Networking.SendToAll("GetPlayerState", "castHub");
    }

    public void PlayerState(CastPlayerState state)
    {
        Networking.Networking.SendToAll("PlayerState", "castHub", state);
    }

    public void SetAudioTrack(int audioTrack)
    {
        Networking.Networking.SendToAll("SetAudioTrack", "castHub", audioTrack);
    }

    public void SetSubtitleTrack(int subtitleTrack)
    {
        Networking.Networking.SendToAll("SetSubtitleTrack", "castHub", subtitleTrack);
    }

    public void SetPlaylistItem(int item)
    {
        Networking.Networking.SendToAll("SetPlaylistItem", "castHub", item);
    }

    public void SetVolume(int volume)
    {
        Networking.Networking.SendToAll("SetVolume", "castHub", volume);
    }

    public void SetMuted(bool muted)
    {
        Networking.Networking.SendToAll("SetMuted", "castHub", muted);
    }

    public void SetSeek(int time)
    {
        Networking.Networking.SendToAll("SetSeek", "castHub", time);
    }

    public void SetNext()
    {
        Networking.Networking.SendToAll("SetNext", "castHub");
    }

    public void SetPrevious()
    {
        Networking.Networking.SendToAll("SetPrevious", "castHub");
    }

    public void SetPlay()
    {
        Networking.Networking.SendToAll("SetPlay", "castHub");
    }

    public void SetPause()
    {
        Networking.Networking.SendToAll("SetPause", "castHub");
    }

    public void SetStop()
    {
        Networking.Networking.SendToAll("SetStop", "castHub");
    }
}