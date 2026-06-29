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
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Api.WebSockets;
using NoMercy.Authorization;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Networking;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.Networking.Http;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Cast;

namespace NoMercy.Api.Hubs;

public partial class MusicHub
{
    /// <summary>
    /// Returns the Client belonging to the connection that invoked this hub
    /// method. Does NOT mutate CurrentDevice — use this when you need to log
    /// who triggered an action but do not want to promote them to active.
    /// </summary>
    private Device GetCallerDevice(User user)
    {
        if (!ConnectedClients.Clients.TryGetValue(Context.ConnectionId, out Client? device))
            throw new InvalidOperationException(
                $"Connection {Context.ConnectionId} not found in ConnectedClients"
            );
        return device;
    }

    /// <summary>
    /// Returns the user's current active device. If no active device is
    /// recorded, or the recorded device has disconnected, the caller is
    /// promoted to active. Otherwise the existing active is preserved —
    /// passive callers do NOT steal active away from a live target.
    /// </summary>
    private Device GetOrPromoteActiveDevice(User user)
    {
        Device caller = GetCallerDevice(user);

        if (CurrentDevice.TryGetValue(user.Id, out Device? existing) && existing is not null)
        {
            bool existingStillConnected = ConnectedClients.Clients.Values.Any(c =>
                c.DeviceId.Equals(existing.DeviceId, StringComparison.OrdinalIgnoreCase)
            );
            if (existingStillConnected)
                return existing;
        }

        CurrentDevice[user.Id] = caller;
        return caller;
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
        User? user = UserCacheService.GetUser(Context.User.UserId());
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
                    string? receiverName = await _chromeCast.FindReceiverNameByIpAsync(ip);
                    if (!string.IsNullOrEmpty(receiverName))
                        await _chromeCast.SelectChromecast(receiverName);
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
            state.VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent;
        }

        UpdateDeviceVolumes(state, device.Sub);
    }

    private void UpdateDeviceVolumes(MusicPlayerState state, Guid userSub)
    {
        Dictionary<string, int> volumes = new();
        foreach (Client client in ConnectedClients.Clients.Values)
        {
            if (!client.Sub.Equals(userSub))
                continue;
            volumes[client.DeviceId] = client.VolumePercent ?? Device.DefaultVolumePercent;
        }

        state.DeviceVolumes = volumes;
    }

    public async Task ChangeDeviceCommand(string? deviceId)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        if (string.IsNullOrEmpty(deviceId))
        {
            _logger.LogWarning(
                "{Name}: [MusicHub.ChangeDeviceCommand] ignored — deviceId was null/empty",
                user.Name
            );
            return;
        }

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
            //
            // The LAUNCH payload now carries a LaunchCustomData bundle: APK
            // ignores its auth fields (already authenticated), Web Receiver
            // consumes them to bootstrap volatile in-memory auth on TVs that
            // don't have the APK installed.
            string targetIp = targetTv.Ip;
            Ulid targetUlid = targetTv.Id;
            string serverIdString = Info.DeviceId.ToString();
            string serverUrl = ResolveServerUrl();
            string locale = ResolveSenderLocale();
            CastIntent intent = ResolveMusicIntent(user.Id, deviceId);

            _ = Task.Run(async () =>
            {
                try
                {
                    string? receiverName = await _chromeCast.FindReceiverNameByIpAsync(targetIp);
                    if (string.IsNullOrEmpty(receiverName))
                    {
                        _logger.LogWarning(
                            "No Chromecast receiver discovered at {TargetIp} — panel won't wake via CEC",
                            targetIp
                        );
                        return;
                    }

                    LaunchCustomData? launchData = await _castTokenService.MintAsync(
                        userId: user.Id,
                        serverId: serverIdString,
                        serverUrl: serverUrl,
                        deviceId: targetUlid,
                        intent: intent,
                        clientLocale: locale
                    );

                    if (launchData is null)
                    {
                        _logger.LogWarning(
                            "Cast token mint failed for {TargetIp} — falling back to LAUNCH without customData",
                            targetIp
                        );
                    }

                    // SelectChromecast connects/reuses the pool entry for this
                    // specific receiver. useAndroidReceiver is true only when
                    // the APK is reachable on this TV (registered with the bus
                    // registry); otherwise cast_shell would try the Cast
                    // Connect path, fail to find the APK, and fall back to Web
                    // Receiver — that fallback path drops customData and the
                    // receiver hangs on its splash. Going straight to Web
                    // Receiver preserves customData.
                    bool apkOnline = _busRegistry.IsOnline(targetUlid);
                    await _chromeCast.SelectChromecast(receiverName);
                    await _chromeCast.LaunchAndroidReceiver(
                        receiverName,
                        launchData,
                        useAndroidReceiver: apkOnline
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Server-side Cast launch failed for {TargetIp}: {Message}",
                        targetIp,
                        ex.Message
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

        // Keep the CurrentDevice registry in sync with playerState.DeviceId.
        // Without this, CurrentDevice could still point at whoever last
        // promoted themselves (e.g. the web client that initiated this
        // ChangeDevice), while playerState says TV — and downstream calls
        // that consult CurrentDevice would see a stale active.
        Device? targetClient = ConnectedClients.Clients.Values.FirstOrDefault(c =>
            c.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)
        );
        if (targetClient is not null)
            CurrentDevice[user.Id] = targetClient;

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

    public async Task ChangeVolumeCommand(int? volume)
    {
        await SetDeviceVolumeCommand(null, volume);
    }

    public async Task SetDeviceVolumeCommand(string? deviceId, int? volume)
    {
        User? user = UserCacheService.GetUser(Context.User.UserId());
        if (user is null)
            return;

        if (volume is null)
            return;

        int clamped = Math.Clamp(volume.Value, 0, 100);

        Device? target = ResolveVolumeTarget(user.Id, deviceId);
        if (target is null)
            return;

        bool targetIsActive =
            CurrentDevice.TryGetValue(user.Id, out Device? active)
            && active.DeviceId.Equals(target.DeviceId, StringComparison.OrdinalIgnoreCase);

        target.VolumePercent = clamped;

        if (_musicPlayerStateManager.TryGetValue(user.Id, out MusicPlayerState? playerState))
        {
            // The scoped volume_percentage belongs to the active device; only
            // move it when the active device is the one being changed. Either
            // way refresh the device_volumes map so controller sliders update.
            if (targetIsActive)
                playerState.VolumePercentage = clamped;

            UpdateDeviceVolumes(playerState, user.Id);
            await _musicPlaybackService.UpdatePlaybackState(user, playerState);
        }

        // Persist off the critical path — the broadcast already reached clients.
        // An in-line await on ExecuteUpdateAsync added 500+ms of wire latency
        // per volume event on SQLite under load.
        string targetDeviceId = target.DeviceId;
        _ = Task.Run(async () =>
        {
            try
            {
                await using MediaContext mediaContext = await ContextFactory.CreateDbContextAsync();
                await mediaContext
                    .Devices.Where(d => d.DeviceId == targetDeviceId)
                    .ExecuteUpdateAsync(d => d.SetProperty(x => x.VolumePercent, clamped));
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    "SetDeviceVolumeCommand DB persist failed: {Message}",
                    ex.Message
                );
            }
        });
    }

    private Device? ResolveVolumeTarget(Guid userId, string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return CurrentDevice.TryGetValue(userId, out Device? active) ? active : null;

        return ConnectedClients.Clients.Values.FirstOrDefault(client =>
            client.Sub.Equals(userId)
            && client.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)
        );
    }
}
