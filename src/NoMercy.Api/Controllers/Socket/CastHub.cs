using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Api.Controllers.Socket;

public class CastHub : ConnectionHub
{
    private readonly IClientMessenger _clientMessenger;

    public CastHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger
    )
        : base(httpContextAccessor, contextFactory, connectedClients)
    {
        _clientMessenger = clientMessenger;
    }

    public class TimeData
    {
        [JsonProperty("currentTime")]
        public double CurrentTime { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("percentage")]
        public double Percentage { get; set; }

        [JsonProperty("remaining")]
        public double Remaining { get; set; }

        [JsonProperty("currentTimeHuman")]
        public string CurrentTimeHuman { get; set; } = string.Empty;

        [JsonProperty("durationHuman")]
        public string DurationHuman { get; set; } = string.Empty;

        [JsonProperty("remainingHuman")]
        public string RemainingHuman { get; set; } = string.Empty;
    }

    public class TextTrack
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("default")]
        public bool Default { get; set; }

        [JsonProperty("file")]
        public string File { get; set; } = string.Empty;

        [JsonProperty("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty("label")]
        public string? Label { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("ext")]
        public string? Ext { get; set; }
    }

    public class AudioTrack
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;
    }

    public class PlaylistItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("uuid")]
        public string Uuid { get; set; } = string.Empty;

        [JsonProperty("seasonName")]
        public string SeasonName { get; set; } = string.Empty;

        [JsonProperty("progress")]
        public ProgressDto Progress { get; set; } = new();

        [JsonProperty("duration")]
        public string Duration { get; set; } = string.Empty;

        [JsonProperty("file")]
        public string File { get; set; } = string.Empty;

        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("tracks")]
        public TextTrack[] Tracks { get; set; } = [];

        [JsonProperty("withCredentials")]
        public bool WithCredentials { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("season")]
        public int Season { get; set; }

        [JsonProperty("episode")]
        public int Episode { get; set; }

        [JsonProperty("show")]
        public string Show { get; set; } = string.Empty;

        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("logo")]
        public string Logo { get; set; } = string.Empty;

        [JsonProperty("rating")]
        public RatingDto Rating { get; set; } = new();
    }

    public class CastPlayerState
    {
        [JsonProperty("time")]
        public TimeData TimeData { get; set; } = new();

        [JsonProperty("volume")]
        public int Volume { get; set; }

        [JsonProperty("muted")]
        public bool Muted { get; set; }

        [JsonProperty("isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonProperty("playlist")]
        public PlaylistItem[] Playlist { get; set; } = [];

        [JsonProperty("currentPlaylistItem")]
        public PlaylistItem? CurrentPlaylistItem { get; set; }

        [JsonProperty("subtitles")]
        public TextTrack[] Subtitles { get; set; } = [];

        [JsonProperty("currentSubtitleTrack")]
        public TextTrack CurrentSubtitleTextTrack { get; set; } = new();

        [JsonProperty("audioTracks")]
        public AudioTrack[] AudioTracks { get; set; } = [];

        [JsonProperty("currentAudioTrack")]
        public int CurrentAudioTrack { get; set; }
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

    public async Task Play()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Play", "castHub", user.Id);
    }

    public async Task Pause()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Pause", "castHub", user.Id);
    }

    public async Task Time(TimeData time)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Time", "castHub", user.Id, time);
    }

    public async Task Ended()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Ended", "castHub", user.Id);
    }

    public async Task Volume(int volume)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Volume", "castHub", user.Id, volume);
    }

    public async Task Muted(bool muted)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Muted", "castHub", user.Id, muted);
    }

    public async Task Item(PlaylistItem item)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Item", "castHub", user.Id, item);
    }

    public async Task Playlist(PlaylistItem[] item)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("Playlist", "castHub", user.Id, item);
    }

    public async Task SubtitleTracks(TextTrack[] subtitleTracks)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SubtitleTracks", "castHub", user.Id, subtitleTracks);
    }

    public async Task CurrentSubtitleTrack(TextTrack subtitleTrack)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("CurrentSubtitleTrack", "castHub", user.Id, subtitleTrack);
    }

    public async Task AudioTracks(AudioTrack[] audioTrack)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("AudioTracks", "castHub", user.Id, audioTrack);
    }

    public async Task CurrentAudioTrack(AudioTrack audioTrack)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("CurrentAudioTrack", "castHub", user.Id, audioTrack);
    }

    public async Task GetPlayerState()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("GetPlayerState", "castHub", user.Id);
    }

    public async Task PlayerState(CastPlayerState state)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("MusicPlayerState", "castHub", user.Id, state);
    }

    public async Task SetAudioTrack(int audioTrack)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetAudioTrack", "castHub", user.Id, audioTrack);
    }

    public async Task SetSubtitleTrack(int subtitleTrack)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetSubtitleTrack", "castHub", user.Id, subtitleTrack);
    }

    public async Task SetPlaylistItem(int item)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetPlaylistItem", "castHub", user.Id, item);
    }

    public async Task SetVolume(int volume)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetVolume", "castHub", user.Id, volume);
    }

    public async Task SetMuted(bool muted)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetMuted", "castHub", user.Id, muted);
    }

    public async Task SetSeek(int time)
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetSeek", "castHub", user.Id, time);
    }

    public async Task SetNext()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetNext", "castHub", user.Id);
    }

    public async Task SetPrevious()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetPrevious", "castHub", user.Id);
    }

    public async Task SetPlay()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetPlay", "castHub", user.Id);
    }

    public async Task SetPause()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetPause", "castHub", user.Id);
    }

    public async Task SetStop()
    {
        User? user = Context.User.User();
        if (user is null) return;
        await _clientMessenger.SendTo("SetStop", "castHub", user.Id);
    }
}
