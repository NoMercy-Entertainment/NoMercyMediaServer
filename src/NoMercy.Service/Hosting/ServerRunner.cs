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

using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Server;
using NoMercyQueue;

namespace NoMercy.Service.Hosting;

public class ServerRunner : IServerRunner
{
    private readonly ILogger<ServerRunner> _logger;
    private readonly IPortManager _portManager;
    private readonly IShutdownCoordinator _shutdownCoordinator;

    public ServerRunner(
        ILogger<ServerRunner> logger,
        IPortManager portManager,
        IShutdownCoordinator shutdownCoordinator
    )
    {
        _logger = logger;
        _portManager = portManager;
        _shutdownCoordinator = shutdownCoordinator;
    }

    public async Task<bool> RunWithHttpsRestart(
        WebApplication httpHost,
        StartupOptions options,
        BootOrchestrator orchestrator
    )
    {
        IShutdownCoordinator shutdownCoordinator =
            httpHost.Services.GetRequiredService<IShutdownCoordinator>();

        SetupState setupState = httpHost.Services.GetRequiredService<SetupState>();

        // Start the HTTP host
        try
        {
            await httpHost.StartAsync(shutdownCoordinator.Token);
        }
        catch (IOException ex)
            when (ex.InnerException is SocketException
                || ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            )
        {
            bool shouldRetry = await _portManager.HandlePortInUse(
                RuntimeServerSettings.Current.InternalServerPort,
                ex
            );
            await httpHost.DisposeAsync();
            return shouldRetry;
        }

        string setupUrl =
            $"http://localhost:{RuntimeServerSettings.Current.InternalServerPort}/setup";
        _logger.LogInformation(
            "Server is in setup mode. Please complete setup at: {SetupUrl}",
            setupUrl
        );

        // Try to open the browser automatically if running interactively.
        if (!options.RunAsService && AuthManager.IsDesktopEnvironment())
        {
            try
            {
                AuthManager.OpenBrowser(setupUrl);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    "Could not open browser automatically: {Message}",
                    ex.Message
                );
                _logger.LogInformation(
                    "Please open your browser and navigate to: {SetupUrl}",
                    setupUrl
                );
            }
        }

        // Headless environments (Docker, NAS, systemd) cannot open a browser — start
        // the device code flow so the user can authenticate from another device.
        if (options.RunAsService || !AuthManager.IsDesktopEnvironment())
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await orchestrator.StartHeadlessDeviceCodeFlowAsync(
                            shutdownCoordinator.Token
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Headless device code flow error: {Message}",
                            ex.Message
                        );
                    }
                },
                shutdownCoordinator.Token
            );
        }

        // Wait for either setup completion or shutdown.
        // Use the host's ApplicationStopping token so that POST /manage/stop
        // (which calls IHostApplicationLifetime.StopApplication()) also cancels
        // this wait — not just Ctrl+C via shutdownCoordinator.
        CancellationToken hostStopping = httpHost
            .Services.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            shutdownCoordinator.Token,
            hostStopping
        );

        // Run post-auth (registration + cert) in background — this waits for
        // Authenticated state, then runs Phase 3 and transitions to Complete.
        Task postAuthTask = Task.Run(
            async () =>
            {
                try
                {
                    await setupState.WaitForPhaseAsync(SetupPhase.Authenticated, linkedCts.Token);
                    await orchestrator.RunRegistrationAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested
                }
            },
            linkedCts.Token
        );

        // Wait for setup to reach Complete or for the server to be shut down
        try
        {
            await setupState.WaitForPhaseAsync(SetupPhase.Complete, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            await httpHost.StopAsync(TimeSpan.FromSeconds(10));
            await httpHost.DisposeAsync();
            return false;
        }

        // Setup completed — certificate should now be available
        if (!Start.Certificate!.HasValidCertificate())
        {
            _logger.LogInformation(
                "Setup completed but certificate not found — continuing on HTTP"
            );
            await httpHost.WaitForShutdownAsync(shutdownCoordinator.Token);
            await httpHost.DisposeAsync();
            return false;
        }

        _logger.LogInformation("Certificate acquired — restarting with HTTPS...");

        // Give the SSO callback page time to deliver its response to the browser
        await Task.Delay(3000);

        // Gracefully stop the HTTP host
        httpHost.Services.GetRequiredService<IBootStatus>().MarkStopped();
        await httpHost.StopAsync(TimeSpan.FromSeconds(10));
        await httpHost.DisposeAsync();

        // Build and start a new host with HTTPS
        Stopwatch restartStopWatch = new();
        restartStopWatch.Start();

        WebApplication httpsHost = WebHostFactory.Create(options);

        _shutdownCoordinator.RequestShutdown(); // Reset existing (if any)

        IShutdownCoordinator httpsShutdownCoordinator =
            httpsHost.Services.GetRequiredService<IShutdownCoordinator>();
        IPortManager httpsPortManager = httpsHost.Services.GetRequiredService<IPortManager>();

        // The new DI container has a fresh AuthManager and SetupState — load tokens
        // from DB and mark setup complete so the middleware stops blocking requests.
        AuthManager httpsAuthManager = httpsHost.Services.GetRequiredService<AuthManager>();
        bool hasValidToken = await httpsAuthManager.InitializeAsync();

        SetupState httpsSetupState = httpsHost.Services.GetRequiredService<SetupState>();
        httpsSetupState.DetermineInitialPhase(hasValidToken: hasValidToken, isRegistered: true);

        httpsAuthManager.ScheduleBackgroundRefresh(httpsShutdownCoordinator.Token);

        // Force the DI container to instantiate QueueRunner (it's a lazy singleton).
        // The constructor sets QueueRunner.Current = this.
        QueueRunner httpsQueueRunner = httpsHost.Services.GetRequiredService<QueueRunner>();

        // Initialize queue workers so they can process jobs on the HTTPS host.
        await httpsQueueRunner.Initialize();

        HostLifecycleHooks.Register(httpsHost, restartStopWatch);

        _logger.LogInformation("HTTPS server starting...");
        return await RunHost(httpsHost);
    }

    public async Task<bool> RunHost(WebApplication host)
    {
        IShutdownCoordinator shutdownCoordinator =
            host.Services.GetRequiredService<IShutdownCoordinator>();
        try
        {
            await host.RunAsync(shutdownCoordinator.Token);
        }
        catch (IOException ex)
            when (ex.InnerException is SocketException
                || ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            )
        {
            bool shouldRetry = await _portManager.HandlePortInUse(
                RuntimeServerSettings.Current.InternalServerPort,
                ex
            );
            await host.DisposeAsync();
            return shouldRetry;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown completed");
        }

        return false;
    }
}
