// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Api.Hubs;

public class CastHub : ConnectionHub
{
    private readonly IClientMessenger _clientMessenger;

    private readonly IAuthTokenStore _authTokenStore;

    private readonly IChromeCastService _chromeCast;

    private readonly ILogger<CastHub> _logger;

    public CastHub(
        ILogger<CastHub> logger,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        IActivityLogger activityLogger,
        IAuthTokenStore authTokenStore,
        IChromeCastService chromeCast
    )
        : base(httpContextAccessor: httpContextAccessor, contextFactory: contextFactory, connectedClients: connectedClients, activityLogger: activityLogger)
    {
        _logger = logger;
        _authTokenStore = authTokenStore;
        _clientMessenger = clientMessenger;
        _chromeCast = chromeCast;
    }

    public class TimeData
    {
        [JsonProperty(propertyName: "currentTime")]
        public double CurrentTime { get; set; }

        [JsonProperty(propertyName: "duration")]
        public double Duration { get; set; }

        [JsonProperty(propertyName: "percentage")]
        public double Percentage { get; set; }

        [JsonProperty(propertyName: "remaining")]
        public double Remaining { get; set; }

        [JsonProperty(propertyName: "currentTimeHuman")]
        public string CurrentTimeHuman { get; set; } = string.Empty;

        [JsonProperty(propertyName: "durationHuman")]
        public string DurationHuman { get; set; } = string.Empty;

        [JsonProperty(propertyName: "remainingHuman")]
        public string RemainingHuman { get; set; } = string.Empty;
    }

    public class TextTrack
    {
        [JsonProperty(propertyName: "id")]
        public int Id { get; set; }

        [JsonProperty(propertyName: "default")]
        public bool Default { get; set; }

        [JsonProperty(propertyName: "file")]
        public string File { get; set; } = string.Empty;

        [JsonProperty(propertyName: "kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty(propertyName: "label")]
        public string? Label { get; set; }

        [JsonProperty(propertyName: "language")]
        public string? Language { get; set; }

        [JsonProperty(propertyName: "type")]
        public string? Type { get; set; }

        [JsonProperty(propertyName: "ext")]
        public string? Ext { get; set; }
    }

    public class AudioTrack
    {
        [JsonProperty(propertyName: "id")]
        public int Id { get; set; }

        [JsonProperty(propertyName: "language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty(propertyName: "label")]
        public string Label { get; set; } = string.Empty;
    }

    public class PlaylistItem
    {
        [JsonProperty(propertyName: "id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty(propertyName: "uuid")]
        public string Uuid { get; set; } = string.Empty;

        [JsonProperty(propertyName: "seasonName")]
        public string SeasonName { get; set; } = string.Empty;

        [JsonProperty(propertyName: "progress")]
        public ProgressDto Progress { get; set; } = new();

        [JsonProperty(propertyName: "duration")]
        public string Duration { get; set; } = string.Empty;

        [JsonProperty(propertyName: "file")]
        public string File { get; set; } = string.Empty;

        [JsonProperty(propertyName: "image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty(propertyName: "title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty(propertyName: "tracks")]
        public TextTrack[] Tracks { get; set; } = [];

        [JsonProperty(propertyName: "withCredentials")]
        public bool WithCredentials { get; set; }

        [JsonProperty(propertyName: "description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty(propertyName: "season")]
        public int Season { get; set; }

        [JsonProperty(propertyName: "episode")]
        public int Episode { get; set; }

        [JsonProperty(propertyName: "show")]
        public string Show { get; set; } = string.Empty;

        [JsonProperty(propertyName: "year")]
        public int Year { get; set; }

        [JsonProperty(propertyName: "logo")]
        public string Logo { get; set; } = string.Empty;

        [JsonProperty(propertyName: "rating")]
        public RatingDto Rating { get; set; } = new();
    }

    public class CastPlayerState
    {
        [JsonProperty(propertyName: "time")]
        public TimeData TimeData { get; set; } = new();

        [JsonProperty(propertyName: "volume")]
        public int Volume { get; set; }

        [JsonProperty(propertyName: "muted")]
        public bool Muted { get; set; }

        [JsonProperty(propertyName: "isPlaying")]
        public bool IsPlaying { get; set; }

        [JsonProperty(propertyName: "playlist")]
        public PlaylistItem[] Playlist { get; set; } = [];

        [JsonProperty(propertyName: "currentPlaylistItem")]
        public PlaylistItem? CurrentPlaylistItem { get; set; }

        [JsonProperty(propertyName: "subtitles")]
        public TextTrack[] Subtitles { get; set; } = [];

        [JsonProperty(propertyName: "currentSubtitleTrack")]
        public TextTrack CurrentSubtitleTextTrack { get; set; } = new();

        [JsonProperty(propertyName: "audioTracks")]
        public AudioTrack[] AudioTracks { get; set; } = [];

        [JsonProperty(propertyName: "currentAudioTrack")]
        public int CurrentAudioTrack { get; set; }
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        _logger.LogInformation(message: "Cast client connected");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception: exception);
        _logger.LogInformation(message: "Cast client disconnected");
    }

    public string[] GetChromeCasts()
    {
        return _chromeCast.GetChromeCasts();
    }

    public async Task SelectChromecast(string name)
    {
        await _chromeCast.SelectChromecast(name: name);
    }

    public async Task Launch()
    {
        await _chromeCast.Launch();
    }

    public async Task CastPlaylist(string value)
    {
        await _chromeCast.CastPlaylist(value: value, accessToken: _authTokenStore.AccessToken);
    }

    public ChromecastStatus? GetChromecastStatus()
    {
        return _chromeCast.GetChromecastStatus();
    }

    public MediaStatus? GetMediaStatus()
    {
        return _chromeCast.GetMediaStatus();
    }

    public async Task Stop()
    {
        await _chromeCast.Stop();
    }

    public async Task Disconnect()
    {
        await _chromeCast.Disconnect();
    }

    public async Task Play()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Play", endpoint: "castHub", userId: user.Id);
    }

    public async Task Pause()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Pause", endpoint: "castHub", userId: user.Id);
    }

    public async Task Time(TimeData time)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Time", endpoint: "castHub", userId: user.Id, data: time);
    }

    public async Task Ended()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Ended", endpoint: "castHub", userId: user.Id);
    }

    public async Task Volume(int volume)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Volume", endpoint: "castHub", userId: user.Id, data: volume);
    }

    public async Task Muted(bool muted)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Muted", endpoint: "castHub", userId: user.Id, data: muted);
    }

    public async Task Item(PlaylistItem item)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Item", endpoint: "castHub", userId: user.Id, data: item);
    }

    public async Task Playlist(PlaylistItem[] item)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "Playlist", endpoint: "castHub", userId: user.Id, data: item);
    }

    public async Task SubtitleTracks(TextTrack[] subtitleTracks)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SubtitleTracks", endpoint: "castHub", userId: user.Id, data: subtitleTracks);
    }

    public async Task CurrentSubtitleTrack(TextTrack subtitleTrack)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "CurrentSubtitleTrack", endpoint: "castHub", userId: user.Id, data: subtitleTrack);
    }

    public async Task AudioTracks(AudioTrack[] audioTrack)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "AudioTracks", endpoint: "castHub", userId: user.Id, data: audioTrack);
    }

    public async Task CurrentAudioTrack(AudioTrack audioTrack)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "CurrentAudioTrack", endpoint: "castHub", userId: user.Id, data: audioTrack);
    }

    public async Task GetPlayerState()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "GetPlayerState", endpoint: "castHub", userId: user.Id);
    }

    public async Task PlayerState(CastPlayerState state)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "MusicPlayerState", endpoint: "castHub", userId: user.Id, data: state);
    }

    public async Task SetAudioTrack(int audioTrack)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetAudioTrack", endpoint: "castHub", userId: user.Id, data: audioTrack);
    }

    public async Task SetSubtitleTrack(int subtitleTrack)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetSubtitleTrack", endpoint: "castHub", userId: user.Id, data: subtitleTrack);
    }

    public async Task SetPlaylistItem(int item)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetPlaylistItem", endpoint: "castHub", userId: user.Id, data: item);
    }

    public async Task SetVolume(int volume)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetVolume", endpoint: "castHub", userId: user.Id, data: volume);
    }

    public async Task SetMuted(bool muted)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetMuted", endpoint: "castHub", userId: user.Id, data: muted);
    }

    public async Task SetSeek(int time)
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetSeek", endpoint: "castHub", userId: user.Id, data: time);
    }

    public async Task SetNext()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetNext", endpoint: "castHub", userId: user.Id);
    }

    public async Task SetPrevious()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetPrevious", endpoint: "castHub", userId: user.Id);
    }

    public async Task SetPlay()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetPlay", endpoint: "castHub", userId: user.Id);
    }

    public async Task SetPause()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetPause", endpoint: "castHub", userId: user.Id);
    }

    public async Task SetStop()
    {
        User? user = UserCacheService.GetUser(userId: Context.User.UserId());
        if (user is null)
            return;
        await _clientMessenger.SendTo(name: "SetStop", endpoint: "castHub", userId: user.Id);
    }
}
