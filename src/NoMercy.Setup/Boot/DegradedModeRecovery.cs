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

using System.IdentityModel.Tokens.Jwt;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Server;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;

namespace NoMercy.Setup.Boot;

public interface IDegradedModeRecovery
{
    Task StartRecoveryLoop(DeferredTasks tasks);
}

public class DegradedModeRecovery : IDegradedModeRecovery
{
    private readonly IApiKeyLoader _apiKeyLoader;
    private readonly IApiKeyStore _apiKeyStore;
    private readonly IServerRegistrationService _serverRegistrationService;
    private readonly INetworkDiscovery? _networkDiscovery;

    private readonly IAuthTokenStore _authTokenStore;
    private readonly Func<TimeSpan, Task> _delay;

    public DegradedModeRecovery(
        IAuthTokenStore authTokenStore,
        IApiKeyLoader apiKeyLoader,
        IApiKeyStore apiKeyStore,
        IServerRegistrationService serverRegistrationService,
        INetworkDiscovery? networkDiscovery = null
    )
        : this(
            authTokenStore: authTokenStore,
            apiKeyLoader: apiKeyLoader,
            apiKeyStore: apiKeyStore,
            serverRegistrationService: serverRegistrationService,
            networkDiscovery: networkDiscovery,
            delay: null
        ) { }

    /// <summary>
    /// Internal (not exposed on the public constructor): lets NoMercy.Tests.Setup drive
    /// <see cref="StartRecoveryLoop"/> through multiple backoff ticks without the real
    /// wall-clock <see cref="BackoffSchedule"/> (30s-30m) — the only DI-visible constructor
    /// remains the public one above, so production callers and the built-in
    /// ServiceProvider are unaffected.
    /// </summary>
    internal DegradedModeRecovery(
        IAuthTokenStore authTokenStore,
        IApiKeyLoader apiKeyLoader,
        IApiKeyStore apiKeyStore,
        IServerRegistrationService serverRegistrationService,
        INetworkDiscovery? networkDiscovery,
        Func<TimeSpan, Task>? delay
    )
    {
        _authTokenStore = authTokenStore;
        _apiKeyLoader = apiKeyLoader;
        _apiKeyStore = apiKeyStore;
        _serverRegistrationService = serverRegistrationService;
        _networkDiscovery = networkDiscovery;
        _delay = delay ?? Task.Delay;
    }

    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(seconds: 30),
        TimeSpan.FromMinutes(minutes: 1),
        TimeSpan.FromMinutes(minutes: 5),
        TimeSpan.FromMinutes(minutes: 15),
        TimeSpan.FromMinutes(minutes: 30),
    ];

    public async Task StartRecoveryLoop(DeferredTasks tasks)
    {
        int attempt = 0;

        while (!tasks.AllCompleted)
        {
            TimeSpan delay = BackoffSchedule[Math.Min(val1: attempt, val2: BackoffSchedule.Length - 1)];
            await _delay(arg: delay);

            bool hasNetwork = await NetworkProbe.CheckConnectivity();
            if (!hasNetwork)
            {
                attempt++;
                Logger.App(
                    message: $"Network still unavailable. Next retry in {BackoffSchedule[Math.Min(val1: attempt, val2: BackoffSchedule.Length - 1)]}"
                );
                continue;
            }

            Logger.App(message: "Network connectivity restored — executing deferred tasks");

            if (!tasks.BinariesReady)
            {
                await TryProvisionBinariesAsync(tasks: tasks);
            }

            if (!tasks.ApiKeysLoaded)
            {
                try
                {
                    await _apiKeyLoader.LoadKeys();
                    tasks.ApiKeysLoaded = _apiKeyStore.KeysLoaded;
                }
                catch (Exception e)
                {
                    Logger.App(message: $"Deferred ApiInfo failed: {e.Message}", level: LogEventLevel.Warning);
                }
            }

            if (tasks is { Authenticated: false, ApiKeysLoaded: true })
            {
                string? token = _authTokenStore.AccessToken;
                if (string.IsNullOrEmpty(value: token))
                {
                    // Auth not ready — AuthManager background refresh will handle it
                    Logger.App(
                        message: "Auth not ready — waiting for AuthManager background refresh",
                        level: LogEventLevel.Verbose
                    );
                }
                else
                {
                    tasks.Authenticated = true;
                }
            }

            if (!tasks.NetworkDiscovered)
            {
                try
                {
                    if (_networkDiscovery is not null)
                        await _networkDiscovery.DiscoverExternalIpAsync();
                    tasks.NetworkDiscovered = true;
                    ServerPhaseTracker.Current?.MarkComplete(stage: BootStage.Network);
                }
                catch (Exception e)
                {
                    Logger.App(
                        message: $"Deferred network discovery failed: {e.Message}",
                        level: LogEventLevel.Warning
                    );
                }
            }

            if (tasks is { Registered: false, Authenticated: true, NetworkDiscovered: true })
            {
                try
                {
                    // Ensure token is present and not expired before attempting registration.
                    // AuthManager background refresh keeps the token alive; a null/empty check
                    // is not sufficient — nomercy-tv will reject an expired JWT.
                    bool tokenNeedsRefresh = true;

                    string? registrationToken = _authTokenStore.AccessToken;
                    if (!string.IsNullOrEmpty(value: registrationToken))
                    {
                        try
                        {
                            JwtSecurityTokenHandler tokenHandler = new();
                            JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(
                                token: registrationToken
                            );
                            tokenNeedsRefresh =
                                parsedToken.ValidTo <= DateTime.UtcNow.AddSeconds(value: 30);
                        }
                        catch
                        {
                            // Token could not be parsed — treat as expired
                        }
                    }

                    if (tokenNeedsRefresh)
                    {
                        Logger.App(
                            message: "Access token missing or expired before deferred registration — waiting for AuthManager background refresh",
                            level: LogEventLevel.Warning
                        );
                        // Auth not ready — AuthManager background refresh will handle it
                        continue;
                    }

                    await _serverRegistrationService.Init();
                    tasks.Registered = true;
                    ServerPhaseTracker.Current?.MarkComplete(stage: BootStage.Registered);
                }
                catch (InvalidOperationException e) when (e.Message.Contains(value: "cooldown"))
                {
                    // Cooldown active — will retry on next loop iteration
                    Logger.App(message: $"Deferred registration deferred: {e.Message}", level: LogEventLevel.Debug);
                }
                catch (Exception e)
                {
                    Logger.App(message: $"Deferred registration failed: {e.Message}", level: LogEventLevel.Warning);
                }
            }

            if (
                tasks is { ApiKeysLoaded: true, Authenticated: true, NetworkDiscovered: true, SeedsRun: true, Registered: true, BinariesReady: true }
            )
            {
                tasks.AllCompleted = true;
                Logger.App(message: "Full mode restored — all deferred tasks completed");
            }

            attempt++;
        }
    }

    /// <summary>
    /// Retries essential-binary provisioning (ffmpeg and friends) with the recovery
    /// loop's backoff schedule. A transient failure on first boot (GitHub rate limit,
    /// network blip, momentarily-empty release feed) must not permanently strand
    /// <c>BootStage.Binaries</c> — encoding genuinely cannot run without ffmpeg, so this
    /// keeps retrying rather than letting the encoder queues run without it. The stage
    /// is marked complete the moment ffmpeg is actually found on disk, whether that is
    /// because this attempt downloaded it or a previous attempt already had.
    /// </summary>
    /// <remarks>Internal (not private) so <c>NoMercy.Tests.Setup</c> can exercise the
    /// ffmpeg-already-on-disk path directly instead of waiting through the loop's
    /// real backoff delays.</remarks>
    internal static async Task TryProvisionBinariesAsync(DeferredTasks tasks)
    {
        try
        {
            IStorageDriver driver = new LocalStorageDriver();
            IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));

            if (storage.Exists(path: AppFiles.FfmpegPath))
            {
                tasks.BinariesReady = true;
                ServerPhaseTracker.Current?.MarkComplete(stage: BootStage.Binaries);
                Logger.App(message: "FFmpeg found on disk — Binaries boot stage marked complete");
                return;
            }

            Logger.App(
                message: "FFmpeg still not installed — retrying binary provisioning",
                level: LogEventLevel.Warning
            );

            // Retry only ffmpeg here, not the full DownloadAll(). The recovery loop
            // calls this on every backoff tick (30s/1m/5m/15m/30m) — re-fetching all
            // ten dependency repos' releases/latest on each tick turns one transient
            // GitHub rate-limit into a self-inflicted, permanent one. Ffmpeg is the
            // only binary this stage blocks on, so it is the only one retried.
            await new Binaries(driver: driver, storage: storage).DownloadFfmpeg();

            if (storage.Exists(path: AppFiles.FfmpegPath))
            {
                tasks.BinariesReady = true;
                ServerPhaseTracker.Current?.MarkComplete(stage: BootStage.Binaries);
                Logger.App(
                    message: "Deferred binary provisioning succeeded — Binaries boot stage marked complete"
                );
            }
        }
        catch (Exception e)
        {
            Logger.App(
                message: $"Deferred binary provisioning failed: {e.Message} — will retry",
                level: LogEventLevel.Warning
            );
        }
    }
}
