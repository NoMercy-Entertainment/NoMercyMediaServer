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
using Microsoft.Extensions.Primitives;
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
/// Requirement: the setup HTTP surface (SSO exchange, device-code flow, retry, QR
/// code, status polling) must dispatch each known path to its handler and fall back to
/// 503 for anything else, enforce the correct HTTP verb per handler, and — for the
/// state-mutating handlers — never leave <see cref="SetupState"/> in a worse spot than
/// it started when a downstream call (token exchange, device-code request) fails.
/// </summary>
/// <remarks>
/// <see cref="SetupEndpoints"/> builds its own <c>System.Net.Http.HttpClient</c> for
/// every Keycloak call — a real loopback <see cref="LoopbackHttpServer"/> exercises the
/// actual contract. <see cref="SetupTerminalUi.ForceInteractiveForTests"/> is pinned to
/// false so these tests exercise the (production-common) non-interactive/service-mode
/// branch rather than constructing a real console resize-watcher thread.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public sealed class SetupEndpointsTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly SetupState _setupState;
    private readonly string? _originalAppPath;
    private readonly string _tempAppPath;
    private readonly string? _originalTokenClientId;

    public SetupEndpointsTests()
    {
        SetupTerminalUi.ForceInteractiveForTests = false;

        _originalAppPath = Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH");
        _tempAppPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-setupep-{Guid.NewGuid():N}");
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

    private SetupEndpoints BuildEndpoints(IServerRegistrationService? registrationService = null) =>
        new(state: _setupState, authManager: _authManager, serverRegistrationService: registrationService ?? new FakeRegistrationService());

    private static DefaultHttpContext BuildContext(
        string method,
        string path,
        string? body = null,
        string? queryString = null,
        string? accept = null
    )
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        if (queryString is not null)
            context.Request.QueryString = new(value: queryString);
        if (accept is not null)
            context.Request.Headers.Accept = accept;
        if (body is not null)
            context.Request.Body = new MemoryStream(buffer: Encoding.UTF8.GetBytes(s: body));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using StreamReader reader = new(stream: context.Response.Body, encoding: Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class FakeRegistrationService : IServerRegistrationService
    {
        public int InitCallCount;

        public Task Init(int maxRetries = 5)
        {
            InitCallCount++;
            return Task.CompletedTask;
        }

        public Task GetTunnelAvailability() => Task.CompletedTask;
    }

    // ── Dispatcher ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequestAsync_UnknownPath_Returns503()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/nonexistent");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "setup_required", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleRequestAsync_TrailingSlash_IsNormalized()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleSetupPage_Get_ReturnsHtml()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.StartsWith(expectedStartString: "text/html", actualString: context.Response.ContentType);
        Assert.NotEmpty(collection: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleSetupPage_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleEmbeddedResource_SetupCss_Get_ReturnsCss()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/setup.css");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.StartsWith(expectedStartString: "text/css", actualString: context.Response.ContentType);
    }

    [Fact]
    public async Task HandleEmbeddedResource_SetupJs_Get_ReturnsJs()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/setup.js");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.StartsWith(expectedStartString: "application/javascript", actualString: context.Response.ContentType);
    }

    [Fact]
    public async Task HandleEmbeddedResource_WrongMethod_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/setup.css");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleEmbeddedBinary_Favicon_ReturnsIcon()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/favicon.ico");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Equal(expected: "image/x-icon", actual: context.Response.ContentType);
    }

    // ── /setup/config ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleSetupConfig_Get_ReturnsPhaseAndPkceChallenge()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/config");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        string body = ReadBody(context: context);
        Assert.Contains(expectedSubstring: "code_challenge", actualString: body);
        Assert.Contains(expectedSubstring: "pkce_state", actualString: body);
        Assert.Contains(expectedSubstring: "Unauthenticated", actualString: body);
    }

    [Fact]
    public async Task HandleSetupConfig_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/config");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    // ── /setup/status ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleSetupStatus_Json_ReturnsSnapshot()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/status");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        string body = ReadBody(context: context);
        Assert.Contains(expectedSubstring: "is_setup_required", actualString: body);
        Assert.Contains(expectedSubstring: "phase", actualString: body);
    }

    [Fact]
    public async Task HandleSetupStatus_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/status");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleSetupStatus_Sse_StreamsAtLeastOneEventThenStopsOnCancellation()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(
            method: "GET",
            path: "/setup/status",
            accept: "text/event-stream"
        );
        using CancellationTokenSource cts = new();
        context.RequestAborted = cts.Token;

        Task handling = endpoints.HandleRequestAsync(context: context);

        await Task.Delay(millisecondsDelay: 100);
        cts.Cancel();
        await handling.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 5));

        Assert.Equal(expected: "text/event-stream", actual: context.Response.ContentType);
        string body = ReadBody(context: context);
        Assert.Contains(expectedSubstring: "data: ", actualString: body);
    }

    // ── /setup/silent-sso ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleSilentSso_Get_ReturnsPostMessageHtml()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/silent-sso");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "postMessage", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleSilentSso_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/silent-sso");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    // ── POST /setup/exchange ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleExchange_Get_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/exchange");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleExchange_MalformedJsonBody_FromJsonIsForgiving_FallsThroughToMissingCode()
    {
        // string.FromJson<T>() (NoMercy.NmSystem.NewtonSoftConverters.JsonHelper) is
        // deliberately forgiving — malformed JSON returns default(T), not a thrown
        // exception — so this never reaches the outer try/catch's "Invalid request
        // body" branch; it falls through to the null-body "Missing code" check
        // instead. That branch is covered by HandleExchange_MissingCode_Returns400.
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: "{not-json");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Missing code", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleExchange_RequestBodyStreamThrows_ReturnsInvalidRequestBody400()
    {
        // Genuinely exercises the outer try/catch (as opposed to FromJson's own
        // forgiving parse failure above) — an already-disposed body stream makes
        // ReadToEndAsync itself throw ObjectDisposedException.
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange");
        MemoryStream disposedBody = new();
        await disposedBody.DisposeAsync();
        context.Request.Body = disposedBody;

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Invalid request body", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleExchange_MissingCode_Returns400()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: "{}");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Missing code", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleExchange_StateMismatch_Returns400()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        string body = JsonConvert.SerializeObject(value: new { code = "abc", state = "wrong-state" });
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: body);

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Invalid state parameter", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleExchange_NoTokenClientId_Returns400()
    {
        ExternalServicesConfig.Current.TokenClientId = string.Empty;
        SetupEndpoints endpoints = BuildEndpoints();
        string body = JsonConvert.SerializeObject(value: new { code = "abc" });
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: body);

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleExchange_TokenEndpointFails_Returns400()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "{\"error\":\"invalid_grant\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        string body = JsonConvert.SerializeObject(value: new { code = "abc" });
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: body);

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "error", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleExchange_Success_StoresTokensAndReturnsOk()
    {
        string jwt = CreateJwt();
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: AuthResponseJson(accessToken: jwt));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        string body = JsonConvert.SerializeObject(value: new { code = "abc" });
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/exchange", body: body);

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "\"status\": \"ok\"", actualString: ReadBody(context: context));
        Assert.Equal(expected: SetupPhase.Authenticated, actual: _setupState.CurrentPhase);
    }

    // NOTE: HandleExchange's `if (alreadyCompleted) return 409;` branch is not covered
    // by a deterministic test. `_exchangeCompleted` is set true and then reset back to
    // false by RegeneratePkce() — called immediately afterward with no `await` between
    // the two — inside the SAME method invocation (SetupEndpoints.cs, the block right
    // after `await _authManager.StoreTokensAsync(tokens)`). The window where a second,
    // truly concurrent request could observe `true` is a handful of CPU instructions
    // wide; two sequential awaited calls (the only thing reachable through the public
    // HandleRequestAsync API in a test) always see it already reset. Reaching this
    // branch deterministically would need white-box thread coordination (e.g. pausing
    // the SUT mid-method), which the "no mocks of the unit under test" rule rules out.

    // ── POST /setup/device-code ──────────────────────────────────────────────

    [Fact]
    public async Task HandleDeviceCode_Get_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/device-code");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleDeviceCode_NoTokenClientId_Returns503()
    {
        ExternalServicesConfig.Current.TokenClientId = string.Empty;
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/device-code");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status503ServiceUnavailable, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleDeviceCode_Success_ReturnsUserCode()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ =>
            new(
                StatusCode: 200,
                Body: JsonConvert.SerializeObject(
                    value: new DeviceAuthResponse
                    {
                        UserCode = "ABCD-1234",
                        VerificationUri = "https://auth.nomercy.tv/device",
                        VerificationUriComplete = "https://auth.nomercy.tv/device?code=ABCD-1234",
                        DeviceCode = "device-code-xyz",
                        ExpiresIn = 600,
                        Interval = 30,
                    }
                )
            );
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/device-code");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "ABCD-1234", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleDeviceCode_RequestFails_Returns500()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "{\"error\":\"invalid_client\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/device-code");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status500InternalServerError, actual: context.Response.StatusCode);
    }

    // ── POST /setup/retry ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleRetry_Get_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/retry");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleRetry_Unauthenticated_ReturnsUnauthenticatedStatus()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/retry");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "unauthenticated", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleRetry_Authenticated_ReturnsRetrying()
    {
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);

        FakeRegistrationService registration = new();
        SetupEndpoints endpoints = BuildEndpoints(registrationService: registration);
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/retry");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "retrying", actualString: ReadBody(context: context));
    }

    // ── /setup/qr ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleQrCode_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/setup/qr");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleQrCode_MissingDataAndUrl_Returns400()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/setup/qr");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleQrCode_WithDataParam_ReturnsPng()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(
            method: "GET",
            path: "/setup/qr",
            queryString: "?data=https://example.com"
        );

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Equal(expected: "image/png", actual: context.Response.ContentType);
        Assert.True(condition: context.Response.ContentLength > 0);
    }

    [Fact]
    public async Task HandleQrCode_WithUrlParamFallback_ReturnsPng()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(
            method: "GET",
            path: "/setup/qr",
            queryString: "?url=https://example.com"
        );

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
    }

    // ── /sso-callback ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleSsoCallback_Post_Returns405()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "POST", path: "/sso-callback");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status405MethodNotAllowed, actual: context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleSsoCallback_ErrorParam_ReturnsErrorHtmlAndSetsState()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(
            method: "GET",
            path: "/sso-callback",
            queryString: "?error=access_denied&error_description=User+cancelled"
        );

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Authorization Failed", actualString: ReadBody(context: context));
        Assert.NotNull(@object: _setupState.ErrorMessage);
    }

    [Fact]
    public async Task HandleSsoCallback_MissingCode_Returns400Json()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/sso-callback");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Missing authorization code", actualString: ReadBody(context: context));
    }

    [Fact]
    public async Task HandleSsoCallback_StateMismatch_Returns400AndSetsError()
    {
        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(
            method: "GET",
            path: "/sso-callback",
            queryString: "?code=abc&state=totally-wrong"
        );

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: context.Response.StatusCode);
        Assert.NotNull(@object: _setupState.ErrorMessage);
    }

    [Fact]
    public async Task HandleSsoCallback_TokenExchangeFails_ReturnsHtmlErrorAndSetsUnauthenticated()
    {
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 400, Body: "{\"error\":\"invalid_grant\"}");
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/sso-callback", queryString: "?code=abc");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Authentication Failed", actualString: ReadBody(context: context));
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: _setupState.CurrentPhase);
    }

    [Fact]
    public async Task HandleSsoCallback_Success_ReturnsSuccessHtmlAndTransitionsToAuthenticated()
    {
        string jwt = CreateJwt();
        using LoopbackHttpServer server = new();
        server.Handler = _ => new(StatusCode: 200, Body: AuthResponseJson(accessToken: jwt));
        using ExternalServicesConfigScope scope = new(authBaseUrl: server.BaseUrl);

        SetupEndpoints endpoints = BuildEndpoints();
        DefaultHttpContext context = BuildContext(method: "GET", path: "/sso-callback", queryString: "?code=abc");

        await endpoints.HandleRequestAsync(context: context);

        Assert.Equal(expected: StatusCodes.Status200OK, actual: context.Response.StatusCode);
        Assert.Contains(expectedSubstring: "Authentication Successful", actualString: ReadBody(context: context));
        // RunPostAuthRegistration fires fire-and-forget (`_ = Task.Run(...)`) the moment
        // the response is written — it may have already advanced the phase past
        // Authenticated by the time this assertion runs, so assert "at least
        // Authenticated" rather than an exact phase to avoid a race with that
        // background task instead of a real assertion of HandleSsoCallback's own effect.
        Assert.True(
            condition: _setupState.CurrentPhase >= SetupPhase.Authenticated,
            userMessage: $"expected at least Authenticated, was {_setupState.CurrentPhase}"
        );

        // RunPostAuthRegistration fires in the background (Task.Run) — give it a brief
        // window to run against the FakeRegistrationService before disposal tears down
        // NOMERCY_APP_PATH out from under it.
        await Task.Delay(millisecondsDelay: 300);
    }

    // ── Static helper methods (pure) ─────────────────────────────────────────

    [Fact]
    public void BuildCallbackHtml_Error_UsesErrorColorAndTitle()
    {
        string html = SetupEndpoints.BuildCallbackHtml(title: "Oops", message: "Something failed", isError: true);

        Assert.Contains(expectedSubstring: "Oops", actualString: html);
        Assert.Contains(expectedSubstring: "Something failed", actualString: html);
        Assert.Contains(expectedSubstring: "#f08080", actualString: html);
    }

    [Fact]
    public void BuildCallbackHtml_Success_UsesSuccessColor()
    {
        string html = SetupEndpoints.BuildCallbackHtml(title: "Great", message: "All good", isError: false);

        Assert.Contains(expectedSubstring: "Great", actualString: html);
        Assert.Contains(expectedSubstring: "#CBAFFF", actualString: html);
    }

    [Fact]
    public void BuildCallbackHtml_EncodesHtmlInTitleAndMessage()
    {
        string html = SetupEndpoints.BuildCallbackHtml(
            title: "<script>alert(1)</script>",
            message: "<b>bold</b>",
            isError: false
        );

        // The template's OWN redirect <script> tag is legitimate and expected — only
        // the caller-supplied title/message must never appear as raw, unencoded HTML.
        Assert.DoesNotContain(expectedSubstring: "<script>alert(1)</script>", actualString: html);
        Assert.Contains(expectedSubstring: "&lt;script&gt;alert(1)&lt;/script&gt;", actualString: html);
        Assert.Contains(expectedSubstring: "&lt;b&gt;bold&lt;/b&gt;", actualString: html);
    }

    [Fact]
    public void BuildRedirectUri_UsesRequestHostPort()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new(host: "example.local", port: 8443);

        string uri = SetupEndpoints.BuildRedirectUri(request: context.Request);

        Assert.Equal(expected: "http://localhost:8443/sso-callback", actual: uri);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateJwt() =>
        new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
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

    private static string AuthResponseJson(string accessToken) =>
        JsonConvert.SerializeObject(
            value: new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = "refresh-1",
                TokenType = "Bearer",
                ExpiresIn = 3600,
            }
        );
}
