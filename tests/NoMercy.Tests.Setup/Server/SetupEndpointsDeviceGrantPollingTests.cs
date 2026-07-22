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

using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Security;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Setup.Infrastructure;
using NoMercyQueue.Workers;

namespace NoMercy.Tests.Setup.Server;

/// <summary>
/// Requirement: after <c>/setup/device-code</c> hands the user a code, the server must
/// keep polling Keycloak's token endpoint in the background per RFC 8628 — treating
/// <c>authorization_pending</c> as "keep waiting" (not an error), <c>slow_down</c> as
/// "back off, keep polling" (not fatal), any other error code as terminal (stop and
/// surface it), and a genuinely expired device code as a timeout rather than an
/// infinite silent loop. A network exception mid-poll must degrade to the same
/// terminal error state, never crash the background task unobserved.
/// </summary>
/// <remarks>
/// Drives the private <c>PollDeviceGrant</c> loop through the public
/// <c>HandleDeviceCode</c> handler (which starts it fire-and-forget) — there is no
/// direct entry point. Each device response's <c>Interval</c> is set to 1 second (the
/// RFC 8628 minimum <c>Math.Clamp</c> floor already enforces >= 1s), so most scenarios
/// here cost only 1-2 real seconds of wall-clock wait.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class SetupEndpointsDeviceGrantPollingTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly SetupState _setupState;
    private readonly string? _originalAppPath;
    private readonly string _tempAppPath;
    private readonly string? _originalTokenClientId;

    public SetupEndpointsDeviceGrantPollingTests()
    {
        SetupTerminalUi.ForceInteractiveForTests = false;

        _originalAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _tempAppPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-devicegrant-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempAppPath);
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _tempAppPath);
        NoMercy.NmSystem.Information.AppFiles.CreateAppFolders();

        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(serviceProvider: provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        _appContext = new(options: optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new(appContext: _appContext, driver: new LocalStorageDriver(), authTokenStore: new AuthTokenStore());
        _setupState = new();

        using AppDbContext onDisk = new();
        onDisk.Database.EnsureCreated();
        Start.Certificate = new CertificateService(logger: NullLogger<CertificateService>.Instance, httpClientFactory: null!);

        _originalTokenClientId = ExternalServicesConfig.Current.TokenClientId;
        ExternalServicesConfig.Current.TokenClientId = "nomercy-server";

        CronWorker.SignalDatabaseReady(success: true);
    }

    public void Dispose()
    {
        SetupTerminalUi.ForceInteractiveForTests = null;
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
        Start.Certificate = null;
        ExternalServicesConfig.Current.TokenClientId = _originalTokenClientId ?? "nomercy-server";
        Environment.SetEnvironmentVariable(variable: "NOMERCY_APP_PATH", value: _originalAppPath);
        try
        {
            if (Directory.Exists(path: _tempAppPath))
                Directory.Delete(path: _tempAppPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private SetupEndpoints BuildEndpoints() =>
        new(state: _setupState, authManager: _authManager, serverRegistrationService: new NoOpRegistrationService());

    private static DefaultHttpContext BuildPostContext(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string DeviceAuthResponseJson(int expiresIn, int interval) =>
        JsonConvert.SerializeObject(
            value: new DeviceAuthResponse
            {
                UserCode = "ABCD-1234",
                VerificationUri = "https://auth.nomercy.tv/device",
                VerificationUriComplete = "https://auth.nomercy.tv/device?code=ABCD-1234",
                DeviceCode = "device-code-xyz",
                ExpiresIn = expiresIn,
                Interval = interval,
            }
        );

    private sealed class NoOpRegistrationService : IServerRegistrationService
    {
        public Task Init(int maxRetries = 5) => Task.CompletedTask;

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }

    /// <summary>Routes the shared token endpoint by a per-call response queue, since
    /// PollDeviceGrant issues one POST per interval tick to the same URL.</summary>
    private static LoopbackHttpServer BuildPollingServer(
        DeviceAuthResponse deviceResponse,
        Queue<(int Status, string Body)> pollResponses
    )
    {
        LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            // The initial device-code request hits ".../auth/device"; every
            // subsequent poll tick hits the plain token endpoint.
            bool isDeviceCodeRequest = req.Path.EndsWith(value: "/device");
            if (isDeviceCodeRequest)
                return new(StatusCode: 200, Body: JsonConvert.SerializeObject(value: deviceResponse));

            // Every other POST is a poll tick against the token endpoint.
            if (pollResponses.Count == 0)
                return new(StatusCode: 400, Body: "{\"error\":\"authorization_pending\"}");

            (int status, string body) = pollResponses.Dequeue();
            return new(StatusCode: status, Body: body);
        };
        return server;
    }

    [Fact]
    public async Task PollDeviceGrant_AuthorizationPendingThenSuccess_StoresTokensAndAuthenticates()
    {
        string jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
            token: new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
                audience: "nomercy-server",
                claims:
                [
                    new(
                        type: System.Security.Claims.ClaimTypes.NameIdentifier,
                        value: Guid.NewGuid().ToString()
                    ),
                ],
                notBefore: DateTime.UtcNow.AddMinutes(value: -5),
                expires: DateTime.UtcNow.AddHours(value: 1)
            )
        );
        string successBody = JsonConvert.SerializeObject(
            value: new AuthResponse
            {
                AccessToken = jwt,
                RefreshToken = "refresh-1",
                TokenType = "Bearer",
                ExpiresIn = 3600,
            }
        );

        Queue<(int, string)> pollResponses = new();
        pollResponses.Enqueue(item: (400, "{\"error\":\"authorization_pending\"}"));
        pollResponses.Enqueue(item: (200, successBody));

        using LoopbackHttpServer server = BuildPollingServer(
            deviceResponse: new()
            {
                UserCode = "ABCD-1234",
                VerificationUri = "https://auth.nomercy.tv/device",
                VerificationUriComplete = "https://auth.nomercy.tv/device?code=ABCD-1234",
                DeviceCode = "device-code-xyz",
                ExpiresIn = 600,
                Interval = 1,
            },
            pollResponses: pollResponses
        );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildPostContext(path: "/setup/device-code");

        await endpoints.HandleRequestAsync(context: context);
        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);

        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 10);
        while (_setupState.CurrentPhase < SetupPhase.Authenticated && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 100);

        Assert.True(
            condition: _setupState.CurrentPhase >= SetupPhase.Authenticated,
            userMessage: $"expected at least Authenticated, was {_setupState.CurrentPhase}"
        );
    }

    [Fact]
    public async Task PollDeviceGrant_AccessDenied_TransitionsToUnauthenticatedWithError()
    {
        Queue<(int, string)> pollResponses = new();
        pollResponses.Enqueue(
            item: (400, "{\"error\":\"access_denied\",\"error_description\":\"User declined\"}")
        );

        using LoopbackHttpServer server = BuildPollingServer(
            deviceResponse: new()
            {
                UserCode = "ABCD-1234",
                VerificationUri = "https://auth.nomercy.tv/device",
                VerificationUriComplete = "https://auth.nomercy.tv/device?code=ABCD-1234",
                DeviceCode = "device-code-xyz",
                ExpiresIn = 600,
                Interval = 1,
            },
            pollResponses: pollResponses
        );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildPostContext(path: "/setup/device-code");
        await endpoints.HandleRequestAsync(context: context);

        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 10);
        while (_setupState.ErrorMessage is null && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 100);

        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: _setupState.CurrentPhase);
        Assert.NotNull(@object: _setupState.ErrorMessage);
    }

    [Fact]
    public async Task PollDeviceGrant_SlowDown_BacksOffThenSucceeds()
    {
        string jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
            token: new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "https://auth.nomercy.tv/realms/NoMercyTV",
                audience: "nomercy-server",
                claims:
                [
                    new(
                        type: System.Security.Claims.ClaimTypes.NameIdentifier,
                        value: Guid.NewGuid().ToString()
                    ),
                ],
                notBefore: DateTime.UtcNow.AddMinutes(value: -5),
                expires: DateTime.UtcNow.AddHours(value: 1)
            )
        );
        string successBody = JsonConvert.SerializeObject(
            value: new AuthResponse
            {
                AccessToken = jwt,
                RefreshToken = "refresh-1",
                TokenType = "Bearer",
                ExpiresIn = 3600,
            }
        );

        Queue<(int, string)> pollResponses = new();
        pollResponses.Enqueue(item: (400, "{\"error\":\"slow_down\"}"));
        pollResponses.Enqueue(item: (200, successBody));

        using LoopbackHttpServer server = BuildPollingServer(
            deviceResponse: new()
            {
                UserCode = "ABCD-1234",
                VerificationUri = "https://auth.nomercy.tv/device",
                VerificationUriComplete = "https://auth.nomercy.tv/device?code=ABCD-1234",
                DeviceCode = "device-code-xyz",
                ExpiresIn = 600,
                Interval = 1,
            },
            pollResponses: pollResponses
        );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildPostContext(path: "/setup/device-code");
        await endpoints.HandleRequestAsync(context: context);

        // slow_down clamps the NEXT interval to +5s (1 -> 6) before retrying, so this
        // scenario genuinely costs several real seconds.
        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 15);
        while (_setupState.CurrentPhase < SetupPhase.Authenticated && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 200);

        Assert.True(
            condition: _setupState.CurrentPhase >= SetupPhase.Authenticated,
            userMessage: $"expected at least Authenticated, was {_setupState.CurrentPhase}"
        );
    }

    [Fact]
    public async Task PollDeviceGrant_NetworkExceptionMidPoll_TransitionsToUnauthenticatedWithError()
    {
        // Serve the device-code request normally, but abort the connection (a real
        // network-level failure, not a parseable HTTP error) for every poll tick
        // against the token endpoint — deterministic, no scope-swap race against the
        // fire-and-forget background poll task.
        using LoopbackHttpServer server = new();
        server.Handler = req =>
            req.Path.EndsWith(value: "/device")
                ? new(StatusCode: 200, Body: DeviceAuthResponseJson(expiresIn: 600, interval: 1))
                : LoopbackResponse.Aborted();
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildPostContext(path: "/setup/device-code");
        await endpoints.HandleRequestAsync(context: context);

        DateTime deadline = DateTime.UtcNow.AddSeconds(value: 10);
        while (_setupState.ErrorMessage is null && DateTime.UtcNow < deadline)
            await Task.Delay(millisecondsDelay: 100);

        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: _setupState.CurrentPhase);
        Assert.NotNull(@object: _setupState.ErrorMessage);
    }

    [Fact]
    public async Task PollDeviceGrant_AlreadyAuthenticatedBeforeFirstTick_StopsWithoutPolling()
    {
        // If the browser SSO flow completes first (silent SSO racing the device
        // code), the poll loop's own guard must notice and stop rather than
        // clobbering an already-authenticated state.
        int pollCount = 0;
        using LoopbackHttpServer server = new();
        server.Handler = req =>
        {
            if (req.Path.EndsWith(value: "/device"))
                return new(StatusCode: 200, Body: DeviceAuthResponseJson(expiresIn: 600, interval: 1));
            Interlocked.Increment(location: ref pollCount);
            return new(StatusCode: 400, Body: "{\"error\":\"authorization_pending\"}");
        };
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildPostContext(path: "/setup/device-code");
        await endpoints.HandleRequestAsync(context: context);

        // Immediately mark authenticated via the browser-SSO path before the first
        // poll tick (1s later) fires.
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);

        await Task.Delay(millisecondsDelay: 1500);

        Assert.Equal(expected: 0, actual: pollCount);
    }
}
