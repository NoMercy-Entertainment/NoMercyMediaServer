using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

/// <summary>
/// End-to-end tests for the setup flow: fresh install → setup UI →
/// authentication → registration → certificate → HTTPS restart.
/// These tests verify the complete state machine journey and the
/// integration between SetupState, SetupServer, SetupModeMiddleware,
/// and the HTTPS restart signal.
/// </summary>
public class EndToEndSetupFlowTests : IAsyncLifetime
{
    private SetupState _state = null!;
    private SetupServer _server = null!;
    private HttpClient _client = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _state = new();
        _port = GetAvailablePort();
        _server = new(_state, _port);
        await _server.StartAsync();
        _client = new() { BaseAddress = new($"http://localhost:{_port}") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync();
    }

    private static int GetAvailablePort()
    {
        using System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // --- Phase 1: Fresh install state ---

    [Fact]
    public async Task FreshInstall_StartsInUnauthenticatedPhase()
    {
        Assert.Equal(SetupPhase.Unauthenticated, _state.CurrentPhase);
        Assert.True(_state.IsSetupRequired);
        Assert.False(_state.IsAuthenticated);
    }

    [Fact]
    public async Task FreshInstall_SetupPageIsAccessible()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", body);
        Assert.Contains("Login with NoMercy", body);
    }

    [Fact]
    public async Task FreshInstall_ConfigReflectsUnauthenticatedState()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Unauthenticated", (string)data!.phase);
        Assert.Equal("setup_required", (string)data.status);
    }

    [Fact]
    public async Task FreshInstall_NonSetupRoutesReturn503()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/libraries");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // --- Phase 2: Authentication phase transition ---

    [Fact]
    public async Task AuthenticationPhase_ConfigReflectsAuthenticatingState()
    {
        _state.TransitionTo(SetupPhase.Authenticating);

        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Authenticating", (string)data!.phase);
    }

    [Fact]
    public async Task AuthenticatedPhase_ConfigReflectsState()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);

        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Authenticated", (string)data!.phase);
        Assert.True(_state.IsAuthenticated);
    }

    // --- Phase 3: Registration phase ---

    [Fact]
    public async Task RegistrationPhase_StatusReflectsRegistering()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);
        _state.TransitionTo(SetupPhase.Registering);

        using HttpResponseMessage response = await _client.GetAsync("/setup/status");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Registering", (string)data!.phase);
        Assert.True((bool)data.is_setup_required);
    }

    // --- Phase 4: Certificate acquired ---

    [Fact]
    public async Task CertificateAcquiredPhase_StatusReflectsPhase()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);
        _state.TransitionTo(SetupPhase.Registering);
        _state.TransitionTo(SetupPhase.Registered);
        _state.TransitionTo(SetupPhase.CertificateAcquired);

        using HttpResponseMessage response = await _client.GetAsync("/setup/status");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("CertificateAcquired", (string)data!.phase);
        Assert.True((bool)data.is_setup_required);
    }

    // --- Phase 5: Complete ---

    [Fact]
    public async Task CompletePhase_SetupNoLongerRequired()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);
        _state.TransitionTo(SetupPhase.Registering);
        _state.TransitionTo(SetupPhase.Registered);
        _state.TransitionTo(SetupPhase.CertificateAcquired);
        _state.TransitionTo(SetupPhase.Complete);

        Assert.False(_state.IsSetupRequired);
        Assert.Equal(SetupPhase.Complete, _state.CurrentPhase);
    }

    [Fact]
    public async Task CompletePhase_StatusReflectsCompletion()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);
        _state.TransitionTo(SetupPhase.Registering);
        _state.TransitionTo(SetupPhase.Registered);
        _state.TransitionTo(SetupPhase.CertificateAcquired);
        _state.TransitionTo(SetupPhase.Complete);

        using HttpResponseMessage response = await _client.GetAsync("/setup/status");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Complete", (string)data!.phase);
        Assert.False((bool)data.is_setup_required);
    }
}

/// <summary>
/// Tests the SSE event stream through a complete setup flow,
/// verifying all phase transitions are pushed to connected clients.
/// </summary>
public class EndToEndSseFlowTests : IAsyncLifetime
{
    private SetupState _state = null!;
    private SetupServer _server = null!;
    private HttpClient _client = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _state = new();
        _port = GetAvailablePort();
        _server = new(_state, _port);
        await _server.StartAsync();
        _client = new() { BaseAddress = new($"http://localhost:{_port}") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync();
    }

    private static int GetAvailablePort()
    {
        using System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task Sse_ReceivesAllPhaseTransitions_ThroughFullFlow()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/setup/status");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using HttpResponseMessage response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead
        );

        using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader reader = new(stream);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        // Read initial state (Unauthenticated)
        string? line = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(line);
        Assert.Contains("Unauthenticated", line);
        await reader.ReadLineAsync(cts.Token); // blank line

        // Transition through all phases
        SetupPhase[] phases =
        [
            SetupPhase.Authenticating,
            SetupPhase.Authenticated,
            SetupPhase.Registering,
            SetupPhase.Registered,
            SetupPhase.CertificateAcquired,
            SetupPhase.Complete,
        ];

        foreach (SetupPhase phase in phases)
        {
            _state.TransitionTo(phase);

            line = await reader.ReadLineAsync(cts.Token);
            Assert.NotNull(line);
            Assert.StartsWith("data: ", line);

            string json = line!.Substring("data: ".Length);
            dynamic? data = JsonConvert.DeserializeObject<dynamic>(json);
            Assert.Equal(phase.ToString(), (string)data!.phase);

            await reader.ReadLineAsync(cts.Token); // blank line
        }
    }

    [Fact]
    public async Task Sse_ErrorsDuringFlow_AreReflectedInStream()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/setup/status");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using HttpResponseMessage response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead
        );

        using Stream stream = await response.Content.ReadAsStreamAsync();
        using StreamReader reader = new(stream);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));

        // Read initial state
        await reader.ReadLineAsync(cts.Token); // data line
        await reader.ReadLineAsync(cts.Token); // blank line

        // Start authenticating
        _state.TransitionTo(SetupPhase.Authenticating);
        await reader.ReadLineAsync(cts.Token); // data line
        await reader.ReadLineAsync(cts.Token); // blank line

        // Set error (auth failure)
        _state.SetError("Authentication failed: invalid credentials");

        string? errorLine = await reader.ReadLineAsync(cts.Token);
        Assert.NotNull(errorLine);
        string errorJson = errorLine!.Substring("data: ".Length);
        dynamic? errorData = JsonConvert.DeserializeObject<dynamic>(errorJson);
        Assert.Equal("Authentication failed: invalid credentials", (string)errorData!.error);
    }
}

/// <summary>
/// Tests the HTTPS restart signal integration with the setup flow.
/// Verifies that WaitForSetupCompleteAsync is properly triggered
/// when the full setup flow completes.
/// </summary>
public class EndToEndHttpsRestartSignalTests
{
    [Fact]
    public async Task FullFlow_TriggersHttpsRestartSignal()
    {
        SetupState state = new();

        // Start waiting for setup to complete (simulates Program.cs)
        Task setupCompleteTask = state.WaitForSetupCompleteAsync();
        Assert.False(setupCompleteTask.IsCompleted);

        // Simulate the full setup flow (as would happen via SetupServer)
        state.TransitionTo(SetupPhase.Authenticating);
        Assert.False(setupCompleteTask.IsCompleted);

        state.TransitionTo(SetupPhase.Authenticated);
        Assert.False(setupCompleteTask.IsCompleted);

        state.TransitionTo(SetupPhase.Registering);
        Assert.False(setupCompleteTask.IsCompleted);

        state.TransitionTo(SetupPhase.Registered);
        Assert.False(setupCompleteTask.IsCompleted);

        state.TransitionTo(SetupPhase.CertificateAcquired);
        Assert.False(setupCompleteTask.IsCompleted);

        // This is the trigger — Program.cs watches for this
        state.TransitionTo(SetupPhase.Complete);

        await setupCompleteTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(setupCompleteTask.IsCompleted);
    }

    [Fact]
    public async Task FullFlow_WithRetry_EventuallyTriggersSignal()
    {
        SetupState state = new();
        Task setupCompleteTask = state.WaitForSetupCompleteAsync();

        // First attempt: auth succeeds but registration fails
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);

        // Registration fails — fallback to authenticated
        state.SetError("Registration failed: network timeout");
        state.TransitionTo(SetupPhase.Authenticated);
        Assert.False(setupCompleteTask.IsCompleted);

        // Second attempt: registration succeeds
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        state.TransitionTo(SetupPhase.Complete);

        await setupCompleteTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(setupCompleteTask.IsCompleted);
    }
}

/// <summary>
/// Tests the middleware integration through the full setup lifecycle.
/// Verifies that routes are properly gated during setup and opened after.
/// </summary>
public class EndToEndMiddlewareFlowTests
{
    private static SetupEndpoints CreateSetupEndpoints(SetupState state)
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        AppDbContext dbContext = new(optionsBuilder.Options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();

        AuthManager authManager = new(dbContext);
        return new SetupEndpoints(state, authManager);
    }

    private static SetupModeMiddleware CreateMiddleware(
        SetupState state,
        RequestDelegate? next = null
    )
    {
        next ??= _ => Task.CompletedTask;
        SetupEndpoints setupEndpoints = CreateSetupEndpoints(state);
        return new(next, state, setupEndpoints);
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task Middleware_BlocksApiRoutes_ThenAllowsAfterSetupComplete()
    {
        SetupState state = new();
        bool nextCalled = false;
        SetupModeMiddleware middleware = CreateMiddleware(
            state,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );

        // During setup: API routes are blocked
        DefaultHttpContext context1 = CreateContext("/api/v1/libraries");
        await middleware.InvokeAsync(context1);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context1.Response.StatusCode);
        Assert.False(nextCalled);

        // Setup routes are handled directly (not blocked, not passed to next)
        nextCalled = false;
        DefaultHttpContext context2 = CreateContext("/setup");
        await middleware.InvokeAsync(context2);
        Assert.False(nextCalled);
        Assert.NotEqual(StatusCodes.Status503ServiceUnavailable, context2.Response.StatusCode);

        // Complete setup
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        state.TransitionTo(SetupPhase.Complete);

        // After setup: API routes are allowed
        nextCalled = false;
        DefaultHttpContext context3 = CreateContext("/api/v1/libraries");
        await middleware.InvokeAsync(context3);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_AllowsHealthRoute_DuringEntireFlow()
    {
        SetupState state = new();
        int healthCallCount = 0;
        SetupModeMiddleware middleware = CreateMiddleware(
            state,
            _ =>
            {
                healthCallCount++;
                return Task.CompletedTask;
            }
        );

        // Health check during setup
        DefaultHttpContext context1 = CreateContext("/health");
        await middleware.InvokeAsync(context1);
        Assert.Equal(1, healthCallCount);

        // Health check after setup
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);
        DefaultHttpContext context2 = CreateContext("/health");
        await middleware.InvokeAsync(context2);
        Assert.Equal(2, healthCallCount);
    }
}

/// <summary>
/// Tests the initial phase determination flow with the bool-based API.
/// (ValidateTokenFile was removed — AuthManager now owns token validation.)
/// </summary>
public class EndToEndTokenValidationFlowTests
{
    [Fact]
    public void NoToken_EntersSetupMode_ThenCanComplete()
    {
        // No valid token → Unauthenticated
        SetupState state = new();
        SetupPhase initialPhase = state.DetermineInitialPhase(hasValidToken: false);
        Assert.Equal(SetupPhase.Unauthenticated, initialPhase);
        Assert.True(state.IsSetupRequired);

        // Setup flow can proceed from Unauthenticated
        Assert.True(state.TransitionTo(SetupPhase.Authenticating));
        Assert.True(state.TransitionTo(SetupPhase.Authenticated));
        Assert.True(state.TransitionTo(SetupPhase.Registering));
        Assert.True(state.TransitionTo(SetupPhase.Registered));
        Assert.True(state.TransitionTo(SetupPhase.CertificateAcquired));
        Assert.True(state.TransitionTo(SetupPhase.Complete));
        Assert.False(state.IsSetupRequired);
    }

    [Fact]
    public void ValidToken_SkipsSetup()
    {
        SetupState state = new();
        SetupPhase initialPhase = state.DetermineInitialPhase(
            hasValidToken: true,
            isRegistered: true
        );
        Assert.Equal(SetupPhase.Complete, initialPhase);
        Assert.False(state.IsSetupRequired);
    }
}

/// <summary>
/// Tests error recovery scenarios in the setup flow.
/// </summary>
public class EndToEndErrorRecoveryTests : IAsyncLifetime
{
    private SetupState _state = null!;
    private SetupServer _server = null!;
    private HttpClient _client = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _state = new();
        _port = GetAvailablePort();
        _server = new(_state, _port);
        await _server.StartAsync();
        _client = new() { BaseAddress = new($"http://localhost:{_port}") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync();
    }

    private static int GetAvailablePort()
    {
        using System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task AuthFailure_ErrorVisibleInStatus_ThenRetrySucceeds()
    {
        // Start authentication
        _state.TransitionTo(SetupPhase.Authenticating);

        // Auth fails — set error (visible before transition clears it)
        _state.SetError("OAuth provider unreachable");

        // Error is visible in status before transition
        using HttpResponseMessage errorResponse = await _client.GetAsync("/setup/status");
        string errorBody = await errorResponse.Content.ReadAsStringAsync();
        dynamic? errorData = JsonConvert.DeserializeObject<dynamic>(errorBody);
        Assert.Equal("OAuth provider unreachable", (string)errorData!.error);

        // Transition back clears the error
        _state.TransitionTo(SetupPhase.Unauthenticated);

        using HttpResponseMessage clearedResponse = await _client.GetAsync("/setup/status");
        string clearedBody = await clearedResponse.Content.ReadAsStringAsync();
        dynamic? clearedData = JsonConvert.DeserializeObject<dynamic>(clearedBody);
        Assert.Null((string?)clearedData!.error);

        // Retry succeeds
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);

        // Error is still cleared after successful transition
        using HttpResponseMessage successResponse = await _client.GetAsync("/setup/status");
        string successBody = await successResponse.Content.ReadAsStringAsync();
        dynamic? successData = JsonConvert.DeserializeObject<dynamic>(successBody);
        Assert.Null((string?)successData!.error);
        Assert.Equal("Authenticated", (string)successData.phase);
    }

    [Fact]
    public async Task RegistrationFailure_FallsBackToAuthenticated()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);
        _state.TransitionTo(SetupPhase.Registering);

        // Registration fails — falls back to Authenticated
        _state.SetError("Server registration failed");
        _state.TransitionTo(SetupPhase.Authenticated);

        Assert.Equal(SetupPhase.Authenticated, _state.CurrentPhase);
        Assert.True(_state.IsSetupRequired);

        // Can retry registration
        _state.TransitionTo(SetupPhase.Registering);
        _state.TransitionTo(SetupPhase.Registered);
        _state.TransitionTo(SetupPhase.CertificateAcquired);
        _state.TransitionTo(SetupPhase.Complete);

        Assert.False(_state.IsSetupRequired);
    }

    [Fact]
    public async Task SsoCallbackWithError_DoesNotTransitionState()
    {
        // OAuth returns an error
        using HttpResponseMessage response = await _client.GetAsync(
            "/sso-callback?error=access_denied&error_description=User+denied+access"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // State remains Unauthenticated
        Assert.Equal(SetupPhase.Unauthenticated, _state.CurrentPhase);

        // Error is set
        Assert.Contains("User denied access", _state.ErrorMessage);
    }
}
