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

    public DegradedModeRecovery(
        IAuthTokenStore authTokenStore,
        IApiKeyLoader apiKeyLoader,
        IApiKeyStore apiKeyStore,
        IServerRegistrationService serverRegistrationService,
        INetworkDiscovery? networkDiscovery = null
    )
    {
        _authTokenStore = authTokenStore;
        _apiKeyLoader = apiKeyLoader;
        _apiKeyStore = apiKeyStore;
        _serverRegistrationService = serverRegistrationService;
        _networkDiscovery = networkDiscovery;
    }

    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
    ];

    public async Task StartRecoveryLoop(DeferredTasks tasks)
    {
        int attempt = 0;

        while (!tasks.AllCompleted)
        {
            TimeSpan delay = BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)];
            await Task.Delay(delay);

            bool hasNetwork = await NetworkProbe.CheckConnectivity();
            if (!hasNetwork)
            {
                attempt++;
                Logger.App(
                    $"Network still unavailable. Next retry in {BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)]}"
                );
                continue;
            }

            Logger.App("Network connectivity restored — executing deferred tasks");

            if (!tasks.BinariesReady)
            {
                await TryProvisionBinariesAsync(tasks);
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
                    Logger.App($"Deferred ApiInfo failed: {e.Message}", LogEventLevel.Warning);
                }
            }

            if (!tasks.Authenticated && tasks.ApiKeysLoaded)
            {
                string? token = _authTokenStore.AccessToken;
                if (string.IsNullOrEmpty(token))
                {
                    // Auth not ready — AuthManager background refresh will handle it
                    Logger.App(
                        "Auth not ready — waiting for AuthManager background refresh",
                        LogEventLevel.Verbose
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
                }
                catch (Exception e)
                {
                    Logger.App(
                        $"Deferred network discovery failed: {e.Message}",
                        LogEventLevel.Warning
                    );
                }
            }

            if (!tasks.Registered && tasks.Authenticated && tasks.NetworkDiscovered)
            {
                try
                {
                    // Ensure token is present and not expired before attempting registration.
                    // AuthManager background refresh keeps the token alive; a null/empty check
                    // is not sufficient — nomercy-tv will reject an expired JWT.
                    bool tokenNeedsRefresh = true;

                    string? registrationToken = _authTokenStore.AccessToken;
                    if (!string.IsNullOrEmpty(registrationToken))
                    {
                        try
                        {
                            JwtSecurityTokenHandler tokenHandler = new();
                            JwtSecurityToken parsedToken = tokenHandler.ReadJwtToken(
                                registrationToken
                            );
                            tokenNeedsRefresh =
                                parsedToken.ValidTo <= DateTime.UtcNow.AddSeconds(30);
                        }
                        catch
                        {
                            // Token could not be parsed — treat as expired
                        }
                    }

                    if (tokenNeedsRefresh)
                    {
                        Logger.App(
                            "Access token missing or expired before deferred registration — waiting for AuthManager background refresh",
                            LogEventLevel.Warning
                        );
                        // Auth not ready — AuthManager background refresh will handle it
                        continue;
                    }

                    await _serverRegistrationService.Init();
                    tasks.Registered = true;
                }
                catch (InvalidOperationException e) when (e.Message.Contains("cooldown"))
                {
                    // Cooldown active — will retry on next loop iteration
                    Logger.App($"Deferred registration deferred: {e.Message}", LogEventLevel.Debug);
                }
                catch (Exception e)
                {
                    Logger.App($"Deferred registration failed: {e.Message}", LogEventLevel.Warning);
                }
            }

            if (
                tasks.ApiKeysLoaded
                && tasks.Authenticated
                && tasks.NetworkDiscovered
                && tasks.SeedsRun
                && tasks.Registered
                && tasks.BinariesReady
            )
            {
                tasks.AllCompleted = true;
                Logger.App("Full mode restored — all deferred tasks completed");
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
            IStorage storage = new LocalStorage(driver, new([], driver));

            if (storage.Exists(AppFiles.FfmpegPath))
            {
                tasks.BinariesReady = true;
                ServerPhaseTracker.Current?.MarkComplete(BootStage.Binaries);
                Logger.App("FFmpeg found on disk — Binaries boot stage marked complete");
                return;
            }

            Logger.App(
                "FFmpeg still not installed — retrying binary provisioning",
                LogEventLevel.Warning
            );

            await new Binaries(driver, storage).DownloadAll();

            if (storage.Exists(AppFiles.FfmpegPath))
            {
                tasks.BinariesReady = true;
                ServerPhaseTracker.Current?.MarkComplete(BootStage.Binaries);
                Logger.App(
                    "Deferred binary provisioning succeeded — Binaries boot stage marked complete"
                );
            }
        }
        catch (Exception e)
        {
            Logger.App(
                $"Deferred binary provisioning failed: {e.Message} — will retry",
                LogEventLevel.Warning
            );
        }
    }
}
