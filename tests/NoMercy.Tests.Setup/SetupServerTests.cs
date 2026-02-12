using System.Net;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

public class SetupServerTests : IAsyncLifetime
{
    private SetupState _state = null!;
    private SetupServer _server = null!;
    private HttpClient _client = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        _state = new SetupState();
        _port = GetAvailablePort();
        _server = new SetupServer(_state, _port);
        await _server.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };
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

    // --- Lifecycle ---

    [Fact]
    public void IsRunning_AfterStart_ReturnsTrue()
    {
        Assert.True(_server.IsRunning);
    }

    [Fact]
    public async Task IsRunning_AfterStop_ReturnsFalse()
    {
        await _server.StopAsync();
        Assert.False(_server.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        await _server.StartAsync();
        Assert.True(_server.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        await _server.StopAsync();
        await _server.StopAsync();
        Assert.False(_server.IsRunning);
    }

    // --- /setup endpoint ---

    [Fact]
    public async Task Setup_ReturnsOk_WithSetupStatus()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.NotNull(data);
        Assert.Equal("setup_required", (string)data!.status);
        Assert.Equal("Unauthenticated", (string)data.phase);
    }

    [Fact]
    public async Task Setup_ReflectsCurrentPhase()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);

        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Authenticated", (string)data!.phase);
    }

    [Fact]
    public async Task Setup_ReflectsErrorMessage()
    {
        _state.SetError("Test error");

        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Test error", (string)data!.error);
    }

    [Fact]
    public async Task Setup_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync("/setup",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- /setup/status endpoint ---

    [Fact]
    public async Task SetupStatus_ReturnsOk_WithPhaseInfo()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.NotNull(data);
        Assert.Equal("Unauthenticated", (string)data!.phase);
        Assert.True((bool)data.is_setup_required);
        Assert.False((bool)data.is_authenticated);
    }

    [Fact]
    public async Task SetupStatus_ReflectsAuthenticatedState()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);

        using HttpResponseMessage response = await _client.GetAsync("/setup/status");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.True((bool)data!.is_authenticated);
        Assert.Equal("Authenticated", (string)data.phase);
    }

    [Fact]
    public async Task SetupStatus_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync("/setup/status",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- /sso-callback endpoint ---

    [Fact]
    public async Task SsoCallback_WithoutCode_Returns400()
    {
        using HttpResponseMessage response = await _client.GetAsync("/sso-callback");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("error", (string)data!.status);
        Assert.Equal("Missing authorization code", (string)data.message);
    }

    [Fact]
    public async Task SsoCallback_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync("/sso-callback",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- 503 for unknown routes ---

    [Fact]
    public async Task UnknownRoute_Returns503()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/libraries");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("setup_required", (string)data!.status);
        Assert.Equal("/setup", (string)data.setup_url);
    }

    [Fact]
    public async Task Root_Returns503()
    {
        using HttpResponseMessage response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task RandomPath_Returns503()
    {
        using HttpResponseMessage response = await _client.GetAsync("/some/random/path");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // --- Trailing slash handling ---

    [Fact]
    public async Task Setup_WithTrailingSlash_ReturnsOk()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetupStatus_WithTrailingSlash_ReturnsOk()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/status/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class SetupServerConstructorTests
{
    [Fact]
    public void Constructor_AcceptsCustomPort()
    {
        SetupState state = new();
        SetupServer server = new(state, 9999);
        Assert.False(server.IsRunning);
    }

    [Fact]
    public void Constructor_DefaultsToConfigPort()
    {
        SetupState state = new();
        SetupServer server = new(state);
        Assert.False(server.IsRunning);
    }
}
