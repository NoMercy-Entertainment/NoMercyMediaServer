using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.Networking;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Hubs;

public class MusicHub : ConnectionHub
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientMessenger _clientMessenger;
    private readonly MusicPlaybackService _musicPlaybackService;
    private readonly MusicPlayerStateManager _musicPlayerStateManager;
    private readonly MusicDeviceManager _musicDeviceManager;
    private readonly MusicPlaylistManager _musicPlaylistManager;
    private readonly MusicPlaybackCommandHandler _commandHandler;
    private readonly NoMercy.Api.Controllers.Socket.DeviceBusRegistry _busRegistry;

    public MusicHub(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<MediaContext> contextFactory,
        ConnectedClients connectedClients,
        IClientMessenger clientMessenger,
        MusicPlaybackService musicPlaybackService,
        MusicPlayerStateManager musicPlayerStateManager,
        MusicDeviceManager musicDeviceManager,
        MusicPlaylistManager musicPlaylistManager,
        MusicPlaybackCommandHandler commandHandler,
        IActivityLogger activityLogger,
        NoMercy.Api.Controllers.Socket.DeviceBusRegistry busRegistry
    )
        : base(httpContextAccessor, contextFactory, connectedClients, activityLogger)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientMessenger = clientMessenger;
        _musicPlaybackService = musicPlaybackService;
        _musicPlayerStateManager = musicPlayerStateManager;
        _musicDeviceManager = musicDeviceManager;
        _musicPlaylistManager = musicPlaylistManager;
        _commandHandler = commandHandler;
        _busRegistry = busRegistry;
    }

    private static readonly ConcurrentDictionary<Guid, Device> CurrentDevice = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> CommandLocks = new();

    private static SemaphoreSlim GetUserLock(Guid userId)
    {
        return CommandLocks.GetOrAdd(userId, _ => new(1, 1));
    }

    public async Task StartPlaybackCommand(string type, Guid listId, Guid trackId)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        SemaphoreSlim userLock = GetUserLock(user.Id);
        await userLock.WaitAsync();
        try
        {
            string country = GetCountryFromContext();

            (PlaylistTrackDto item, List<PlaylistTrackDto> playlist) =
                await _musicPlaylistManager.GetPlaylist(user.Id, type, listId, trackId, country);
            await HandlePlaybackState(user, type, listId, item, playlist);
        }
        catch (ArgumentException ex)
        {
            Logger.App($"Invalid playlist type: {ex.Message}");

            ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
            Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
            try
            {
                await ActivityLogger.LogFailureAsync(
                    "failure.playback_start",
                    user.Id,
                    deviceId2,
                    errorCode: ex.GetType().Name,
                    message: ex.Message
                );
            }
            catch (Exception logEx)
            {
                Logger.Socket(
                    $"Failed to log failure.playback_start: {logEx.Message}",
                    LogEventLevel.Warning
                );
            }
        }
        catch (Exception ex)
        {
            Logger.App($"Error in StartPlaybackCommand: {ex.Message}");

            ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client2);
            Ulid deviceId2 = client2?.Id ?? Ulid.Empty;
            try
            {
                await ActivityLogger.LogFailureAsync(
                    "failure.playback_start",
                    user.Id,
                    deviceId2,
                    errorCode: ex.GetType().Name,
                    message: ex.Message
                );
            }
            catch (Exception logEx)
            {
                Logger.Socket(
                    $"Failed to log failure.playback_start: {logEx.Message}",
                    LogEventLevel.Warning
                );
            }
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task HandlePlaybackState(
        User user,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        MusicPlayerState? playerState = _musicPlayerStateManager.GetState(user.Id);

        // Special handling for type="track" - only works with existing player state
        if (type.ToLower().Trim() == "track")
        {
            if (playerState?.CurrentItem is null)
            {
                // No active player state, cannot reorder - log and return
                Logger.App("Cannot play track: No active playlist");
                return;
            }
            await HandleTrackReorder(user, playerState, item);
            return;
        }

        // Normal playlist handling
        if (playerState?.CurrentItem is null || playerState.Playlist.Count == 0)
            await HandleNewPlayerState(user, type, listId, item, playlist);
        else if (IsSamePlaylistAndTrack(playerState, type, listId, item.Id))
            await HandleExistingPlaylistState(user, playerState);
        else if (IsSamePlaylist(playerState, type, listId))
            await HandleTrackReorder(user, playerState, item);
        else
            await HandlePlaylistChange(user, playerState, type, listId, item, playlist);
    }

    private async Task HandleNewPlayerState(
        User user,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        Device device = GetCurrentDevice(user);
        MusicPlayerState musicPlayerState = MusicPlayerStateFactory.Create(
            device,
            item,
            playlist,
            type,
            listId
        );
        musicPlayerState.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);

        _musicPlaybackService.RemoveTimer(user.Id);
        _musicPlayerStateManager.UpdateState(user.Id, musicPlayerState);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, musicPlayerState);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, musicPlayerState);

        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                Ulid.Empty,
                new
                {
                    media_type = "audio",
                    track_id = item.Id,
                    title = item.Name,
                }
            );
        }
        catch (Exception ex)
        {
            Logger.Socket($"Failed to log playback.started: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private Device GetCurrentDevice(User user)
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
            throw new InvalidOperationException(
                $"Connection {Context.ConnectionId} not found in ConnectedClients"
            );

        CurrentDevice[user.Id] = device;

        return device;
    }

    /// <summary>
    /// MusicHub-flavoured device list: live MusicHub clients plus every TV the
    /// current user owns from the Devices table, including ones that aren't
    /// currently on the hub (sleeping panels, powered-off boxes). The web and
    /// mobile pickers need to render those so the user can wake them; without
    /// this merge a standby TV silently disappears from the picker until it
    /// reconnects on its own.
    /// </summary>
    private async Task<List<Device>> MusicDevicesAsync()
    {
        List<Device> connected = Devices();
        User? user = Context.User.User();
        if (user is null)
            return connected;

        await using MediaContext ctx = await ContextFactory.CreateDbContextAsync();
        List<Device> registeredTvs = await ctx
            .Devices.Where(d => d.OwnerUserId == user.Id && d.Type == "tv")
            .ToListAsync();

        HashSet<string> seenDeviceIds = new(
            connected.Select(d => d.DeviceId),
            StringComparer.OrdinalIgnoreCase
        );

        foreach (Device tv in registeredTvs)
        {
            if (seenDeviceIds.Add(tv.DeviceId))
                connected.Add(tv);
        }

        // Pre-warm sharpcaster's TLS pool for every owned TV so the first
        // ChangeDeviceCommand to that TV doesn't pay cold-handshake latency
        // (which is the leading cause of first-tap LAUNCH races on the wire).
        // Fire-and-forget; the per-receiver client pool dedupes so repeated
        // calls are cheap.
        foreach (Device tv in registeredTvs)
        {
            if (string.IsNullOrEmpty(tv.Ip))
                continue;
            string ip = tv.Ip;
            _ = Task.Run(async () =>
            {
                try
                {
                    string? receiverName = await ChromeCast.FindReceiverNameByIpAsync(ip);
                    if (!string.IsNullOrEmpty(receiverName))
                        await ChromeCast.SelectChromecast(receiverName);
                }
                catch
                {
                    // Best-effort pre-warm; the actual cast will retry the
                    // discovery + connect path itself if this fails.
                }
            });
        }

        return connected;
    }

    private static bool IsSamePlaylist(MusicPlayerState state, string type, Guid listId)
    {
        return state.CurrentItem is not null
            && state.CurrentList.ToString().Contains($"{type}/{listId}");
    }

    private static bool IsSamePlaylistAndTrack(
        MusicPlayerState state,
        string type,
        Guid listId,
        Guid itemId
    )
    {
        return IsSamePlaylist(state, type, listId) && state.CurrentItem?.Id == itemId;
    }

    private async Task HandleExistingPlaylistState(User user, MusicPlayerState state)
    {
        state.PlayState = !state.PlayState;
        UpdateActionsDisallows(state);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
        if (state.PlayState)
        {
            await _musicPlaybackService.PublishStartedEventAsync(user.Id, state);
        }
    }

    private async Task HandleTrackReorder(User user, MusicPlayerState state, PlaylistTrackDto item)
    {
        // Stop the old timer before modifying state to prevent race conditions
        _musicPlaybackService.RemoveTimer(user.Id);

        // Check if it's the current item
        if (state.CurrentItem?.Id == item.Id)
        {
            // Already playing this track, just restart it
            state.Time = 0;
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
            state.PlayState = true;
            UpdateActionsDisallows(state);
            _musicPlaybackService.StartPlaybackTimer(user);
            await _musicPlaybackService.UpdatePlaybackState(user, state);
            return;
        }

        // Find the track in the current playlist
        int playlistIndex = state.Playlist.FindIndex(t => t.Id == item.Id);

        if (playlistIndex != -1)
        {
            // Track is in the upcoming playlist
            // Add current item to backlog
            if (state.CurrentItem != null)
            {
                state.Backlog.Add(state.CurrentItem);
            }

            // Add all tracks BEFORE the selected one to backlog (they're being skipped over)
            for (int i = 0; i < playlistIndex; i++)
            {
                state.Backlog.Add(state.Playlist[i]);
            }

            // Remove everything up to and including the selected track
            state.Playlist.RemoveRange(0, playlistIndex + 1);

            // Set the selected track as current
            // The remaining playlist continues naturally from here
            state.CurrentItem = item;
            state.Time = 0;
            state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
            state.PlayState = true;
            state.Duration = item.Duration.ToMilliSeconds();
        }
        else
        {
            // Check if track is in backlog (going backwards)
            int backlogIndex = state.Backlog.FindIndex(t => t.Id == item.Id);

            if (backlogIndex != -1)
            {
                // Track is in backlog - going backwards
                // Remove it from backlog
                state.Backlog.RemoveAt(backlogIndex);

                // Add current item to backlog
                if (state.CurrentItem != null)
                {
                    state.Backlog.Add(state.CurrentItem);
                }

                // Set the selected track as current
                state.CurrentItem = item;
                state.Time = 0;
                state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
                state.PlayState = true;
                state.Duration = item.Duration.ToMilliSeconds();
            }
            else
            {
                // Track not found in current queue at all
                Logger.App($"Track {item.Id} not found in current queue");
                return;
            }
        }

        UpdateActionsDisallows(state);
        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
    }

    private async Task HandlePlaylistChange(
        User user,
        MusicPlayerState state,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        _musicPlaybackService.RemoveTimer(user.Id);

        UpdateDeviceInfo(state);
        UpdatePlaylistInfo(state, type, listId, item, playlist);
        UpdateActionsDisallows(state);

        _musicPlaybackService.StartPlaybackTimer(user);
        await _musicPlaybackService.UpdatePlaybackState(user, state);
        await _musicPlaybackService.PublishStartedEventAsync(user.Id, state);

        Device device = GetCurrentDevice(user);
        try
        {
            await ActivityLogger.LogPlaybackAsync(
                "playback.started",
                user.Id,
                device.Id,
                Ulid.Empty,
                new
                {
                    media_type = "audio",
                    track_id = item.Id,
                    title = item.Name,
                }
            );
        }
        catch (Exception ex)
        {
            Logger.Socket($"Failed to log playback.started: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private void UpdateDeviceInfo(MusicPlayerState state)
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
            return;

        // Only adopt the caller's device as active when there is no active
        // device yet, or when the caller IS the current active. A passive
        // device that initiates a playlist change (e.g. phone tapping an
        // album while music plays on the TV) must NOT steal active back —
        // the new playlist should land on the existing active device.
        bool callerIsActiveOrNoActive =
            string.IsNullOrEmpty(state.DeviceId)
            || state.DeviceId.Equals(device.DeviceId, StringComparison.OrdinalIgnoreCase);

        if (callerIsActiveOrNoActive)
        {
            state.DeviceId = device.DeviceId;
            state.VolumePercentage = device.VolumePercent;
        }
    }

    private void UpdatePlaylistInfo(
        MusicPlayerState state,
        string type,
        Guid listId,
        PlaylistTrackDto item,
        List<PlaylistTrackDto> playlist
    )
    {
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) =
            _musicPlaylistManager.SplitPlaylist(playlist, item.Id);
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(after);
        sortedPlaylist.AddRange(before);

        state.CurrentItem = item;
        state.PlayState = true;
        state.Playlist = sortedPlaylist;
        state.CurrentList = new($"/music/{type}/{listId}", UriKind.Relative);
        state.Backlog.Add(item);
        state.Time = 0;
        state.IgnoreCurrentTimeUntil = DateTime.UtcNow.AddSeconds(1);
        state.Duration = item.Duration.ToMilliSeconds();
    }

    private void UpdateActionsDisallows(MusicPlayerState state)
    {
        state.Actions = new()
        {
            Disallows = new()
            {
                // Can't pause if already paused
                Pausing = !state.PlayState,
                // Can't resume if already playing
                Resuming = state.PlayState,
                // Can't go to previous if backlog is empty (no tracks to go back to)
                Previous = state.Backlog.Count <= 0,
                // Can't go to next if playlist is empty and repeat is off
                Next = state.Playlist.Count <= 0 && state.Repeat == "off",
                // Basic actions that are always allowed during playback
                Seeking = false,
                Stopping = false,
                Muting = false,
                TogglingShuffle = false,
                TogglingRepeatContext = false,
                TogglingRepeatTrack = false,
            },
        };
    }

    public MusicPlayerState? GetStateCommand()
    {
        User? user = Context.User.User();
        if (user is null)
            return null;

        _musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState);
        if (playerState is null)
            return null;

        return playerState;
    }

    public async Task PlaybackCommand(string command, object? data = null)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        SemaphoreSlim userLock = GetUserLock(user.Id);
        await userLock.WaitAsync();
        try
        {
            if (!_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? state))
            {
                await _musicPlaybackService.UpdatePlaybackState(user, null);
                return;
            }

            _commandHandler.HandleCommand(user, command, data, state);

            if (state.DeviceId == null)
                if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
                {
                    state.DeviceId = device.DeviceId;
                    state.VolumePercentage = device.VolumePercent;
                }

            UpdateActionsDisallows(state);

            bool isSkipCommand =
                command.Equals("next", StringComparison.OrdinalIgnoreCase)
                || command.Equals("previous", StringComparison.OrdinalIgnoreCase);

            if (isSkipCommand)
                _musicPlaybackService.DebouncedUpdatePlaybackState(user, state);
            else
                await _musicPlaybackService.UpdatePlaybackState(user, state);
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task CurrentTimeCommand(int time)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            if (DateTime.UtcNow < playerState.IgnoreCurrentTimeUntil)
                return;

            playerState.Time = time * 1000;

            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
    }

    /// <summary>
    /// Called by the active client when it begins its crossfade volume ramp (typically 3 s
    /// before the current track ends).  Suppresses the server's 100 ms auto-advance timer for
    /// this user so the server does not race the client and interrupt the fade.
    /// </summary>
    /// <param name="fadeDurationMs">
    /// How long the client's fade takes in milliseconds (e.g. 3000).  The server adds a 5 s
    /// safety margin on top; if <c>CrossfadeCompleteCommand</c> never arrives within that window
    /// the server force-advances anyway.
    /// </param>
    public Task CrossfadeStartCommand(int fadeDurationMs)
    {
        User? user = Context.User.User();
        if (user is null)
            return Task.CompletedTask;

        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            return Task.CompletedTask;

        _musicPlaybackService.StartCrossfade(user.Id, client.DeviceId, fadeDurationMs);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the active client once its crossfade is complete and the new track is fully
    /// playing.  The server advances state to <paramref name="newTrackId"/>, resets progress to
    /// zero, and broadcasts the updated state to all connected clients.
    /// </summary>
    /// <param name="newTrackId">The <see cref="Guid"/> of the track that is now playing.</param>
    public async Task CrossfadeCompleteCommand(Guid newTrackId)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            return;

        await _musicPlaybackService.CompleteCrossfade(user, client.DeviceId, newTrackId);
    }

    public async Task ChangeDeviceCommand(string deviceId)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        List<Device> connectedDevices = await MusicDevicesAsync();

        await _clientMessenger.SendTo(
            "ConnectedDevicesState",
            "musicHub",
            user.Id,
            connectedDevices
        );

        // If the target is a TV that owns the user but isn't currently on
        // MusicHub, fire wake_for_music over the device-bus so its panel +
        // app come up. Without this, the web picker can transfer the active
        // flag to a sleeping TV but the TV never actually plays. Mobile
        // already drives this through DeviceHub.WakeForMusic; web can't, so
        // the server has to do it on their behalf.
        bool targetIsLive = connectedDevices.Any(d =>
            d.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)
            && ConnectedClients.Clients.Values.Any(c =>
                c.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)
                && c.Endpoint.Contains("musicHub", StringComparison.OrdinalIgnoreCase)
            )
        );

        Device? targetTv = connectedDevices.FirstOrDefault(d =>
            d.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase) && d.Type == "tv"
        );

        if (targetTv is not null)
        {
            // Software wake: only when the TV's MusicHub side isn't already live.
            // If it's live the app is already foregrounded; sending wake_for_music
            // again would just redundantly bounce the activity stack.
            if (!targetIsLive && _busRegistry.IsOnline(targetTv.Id))
            {
                _ = _busRegistry.SendAsync(
                    targetTv.Id,
                    new { type = "wake_for_music", session_id = Guid.NewGuid().ToString() }
                );
            }

            // Panel wake (CEC OTP): always fire on every TV-target ChangeDevice,
            // even when the app is already live on MusicHub. The user re-tapping
            // the active TV usually means "my screen went off, wake it again" —
            // cast_shell only fires HDMI-CEC One Touch Play when it receives a
            // Cast LAUNCH, so we issue one server-side via sharpcaster against
            // the discovered Chromecast receiver. Best-effort, async — some TV
            // models / cast_shell builds don't honor third-party LAUNCHes for CEC.
            // Resolve the receiver via its LAN IP rather than name — the Cast
            // mDNS name (set in Android TV settings) doesn't match our DB's
            // custom name (set in NoMercy onboarding). Async because the lookup
            // may need to refresh mDNS discovery if the cache is stale.
            string targetIp = targetTv.Ip;
            _ = Task.Run(async () =>
            {
                try
                {
                    string? receiverName = await ChromeCast.FindReceiverNameByIpAsync(targetIp);
                    if (string.IsNullOrEmpty(receiverName))
                    {
                        Logger.Socket(
                            $"No Chromecast receiver discovered at {targetIp} — panel won't wake via CEC",
                            Serilog.Events.LogEventLevel.Warning
                        );
                        return;
                    }
                    // SelectChromecast connects/reuses the pool entry for this
                    // specific receiver; LaunchAndroidReceiver sends a LAUNCH
                    // with launchOptions.androidReceiverCompatible=true so
                    // cast_shell foregrounds the native APK instead of the
                    // Web Receiver placeholder.
                    await ChromeCast.SelectChromecast(receiverName);
                    await ChromeCast.LaunchAndroidReceiver(receiverName);
                }
                catch (Exception ex)
                {
                    Logger.Socket(
                        $"Server-side Cast launch failed for {targetIp}: {ex.Message}",
                        Serilog.Events.LogEventLevel.Warning
                    );
                }
            });
        }

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            playerState.DeviceId = deviceId;
        }
        else
        {
            // No live player state — nothing to transfer. The else-branch's previous
            // `UpdatePlaybackState(user, null)` call would have NRE'd; just return.
            return;
        }

        EventPayload<BroadcastEventPayload> payload = new()
        {
            Events =
            [
                new()
                {
                    DeviceBroadcastStatus = new()
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        BroadcastStatus = MusicEventType.BroadcastUnavailable,
                        DeviceId = deviceId,
                    },
                },
            ],
        };

        await _clientMessenger.SendTo("ChangeDevice", "musicHub", user.Id, payload);

        // Broadcast the updated playback state so the new active device receives the
        // current track + position and starts playing. Without this, TV becomes the
        // active device flag but stays paused with isPlaying=false.
        await _musicPlaybackService.UpdatePlaybackState(user, playerState);
    }

    public async Task ChangeVolumeCommand(int volume)
    {
        User? user = Context.User.User();
        if (user is null)
            return;

        // Diagnostic: log which device/connection sent this command so we can
        // tell phone vs PC vs TV apart when hunting down echo loops.
        string senderDevice = Context.ConnectionId;
        if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? sender))
            senderDevice = $"{sender.Name}/{sender.DeviceId}/{sender.Browser}";
        Logger.App($"ChangeVolumeCommand {volume} from {senderDevice}");

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            playerState.VolumePercentage = volume;
            // Fire the broadcast FIRST so clients see the new value with the
            // minimum possible latency. The in-memory state is already
            // authoritative for future broadcasts.
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
            return;
        }

        // Persist to the ACTIVE device off the critical path. Clients don't
        // need the DB row to land before they can react — the broadcast
        // already reached them. Previous in-line await on ExecuteUpdateAsync
        // added 500+ms of wire latency per volume event on SQLite under load.
        if (CurrentDevice.TryGetValue(user.Id, out Device? device))
        {
            device.VolumePercent = volume;
            string deviceId = device.DeviceId;
            _ = Task.Run(async () =>
            {
                try
                {
                    await using MediaContext mediaContext = new();
                    await mediaContext
                        .Devices.Where(d => d.DeviceId == deviceId)
                        .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, volume));
                }
                catch (Exception ex)
                {
                    Logger.App($"ChangeVolumeCommand DB persist failed: {ex.Message}");
                }
            });
        }
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        User? user = Context.User.User();
        if (user is null)
            return;

        await Task.Delay(500);

        // Send updated device list to all connected devices for this user
        List<Device> connectedDevices = await MusicDevicesAsync();
        await _clientMessenger.SendTo(
            "ConnectedDevicesState",
            "musicHub",
            user.Id,
            connectedDevices
        );

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            UpdateActionsDisallows(playerState);
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }
        else
        {
            await _musicPlaybackService.UpdatePlaybackState(user, new());
        }

        Logger.Socket("Music client connected", LogEventLevel.Debug);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        User? user = Context.User.User();
        if (user == null)
            return;

        bool stopPlayback = false;
        bool wasCurrentDevice = false;
        Ulid stoppedDeviceId = Ulid.Empty;
        Guid stoppedTrackId = Guid.Empty;
        string? stoppedTitle = null;

        if (ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? client))
            if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? state))
                if (state.DeviceId == client.DeviceId)
                {
                    _musicPlaybackService.RemoveTimer(user.Id);

                    _musicDeviceManager.RemoveUserDevice(user.Id);

                    stopPlayback = true;
                    wasCurrentDevice = true;
                    stoppedDeviceId = client.Id;
                    stoppedTrackId = state.CurrentItem?.Id ?? Guid.Empty;
                    stoppedTitle = state.CurrentItem?.Name;
                }

        await base.OnDisconnectedAsync(exception);

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            List<Device> connectedDevices = await MusicDevicesAsync();

            // Send updated device list to all remaining connected devices
            await _clientMessenger.SendTo(
                "ConnectedDevicesState",
                "musicHub",
                user.Id,
                connectedDevices
            );

            if (connectedDevices.Count == 0)
            {
                CurrentDevice.TryRemove(user.Id, out _);

                // Clean up CommandLock and player state — no connections remain for this user
                if (CommandLocks.TryRemove(user.Id, out SemaphoreSlim? removedLock))
                    removedLock.Dispose();

                _musicPlayerStateManager.RemoveState(user.Id);
                playerState = null;
            }
            else if (stopPlayback)
            {
                // Remove current device if it was the disconnecting device
                if (wasCurrentDevice)
                {
                    CurrentDevice.TryRemove(user.Id, out _);
                }

                playerState.PlayState = false;
                playerState.Actions = new()
                {
                    Disallows = new()
                    {
                        Pausing = true,
                        Resuming = false,
                        Previous =
                            playerState.CurrentItem == null || playerState.Backlog.Count <= 1,
                        Next =
                            playerState.CurrentItem == null
                            || (
                                playerState.Playlist.IndexOf(playerState.CurrentItem)
                                    >= playerState.Playlist.Count - 1
                                && playerState.Repeat == "off"
                            ),
                        Seeking = false,
                        Stopping = false,
                        Muting = false,
                        TogglingShuffle = false,
                        TogglingRepeatContext = false,
                        TogglingRepeatTrack = false,
                    },
                };
            }
        }

        await _musicPlaybackService.UpdatePlaybackState(user, playerState);

        if (stopPlayback && stoppedDeviceId != Ulid.Empty)
        {
            try
            {
                await ActivityLogger.LogPlaybackAsync(
                    "playback.stopped",
                    user.Id,
                    stoppedDeviceId,
                    Ulid.Empty,
                    new
                    {
                        media_type = "audio",
                        track_id = stoppedTrackId,
                        title = stoppedTitle,
                    }
                );
            }
            catch (Exception ex)
            {
                Logger.Socket(
                    $"Failed to log playback.stopped: {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }

        Logger.Socket("Music client disconnected", LogEventLevel.Debug);
    }
}
