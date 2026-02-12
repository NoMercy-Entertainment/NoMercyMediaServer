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

    // --- /setup endpoint (now serves HTML) ---

    [Fact]
    public async Task Setup_ReturnsOk_WithHtmlContent()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", body);
        Assert.Contains("NoMercy", body);
        Assert.Contains("MediaServer Setup", body);
    }

    [Fact]
    public async Task Setup_HtmlContainsLoginButton()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Login with NoMercy", body);
        Assert.Contains("btn-login", body);
    }

    [Fact]
    public async Task Setup_HtmlContainsDeviceGrantSection()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("device-code", body);
        Assert.Contains("qr-container", body);
        Assert.Contains("btn-device-grant", body);
    }

    [Fact]
    public async Task Setup_HtmlContainsProgressSection()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("step-progress", body);
        Assert.Contains("spinner", body);
    }

    [Fact]
    public async Task Setup_HtmlContainsCompleteSection()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Contains("step-complete", body);
        Assert.Contains("Setup Complete", body);
    }

    [Fact]
    public async Task Setup_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync("/setup",
            new StringContent(""));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- /setup/config endpoint ---

    [Fact]
    public async Task SetupConfig_ReturnsOk_WithConfigJson()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.NotNull(data);
        Assert.Equal("setup_required", (string)data!.status);
        Assert.Equal("Unauthenticated", (string)data.phase);
        Assert.NotNull((string)data.auth_base_url);
        Assert.NotNull((string)data.client_id);
        Assert.NotNull((string)data.code_challenge);
        Assert.NotEmpty((string)data.code_challenge);
    }

    [Fact]
    public async Task SetupConfig_ReflectsCurrentPhase()
    {
        _state.TransitionTo(SetupPhase.Authenticating);
        _state.TransitionTo(SetupPhase.Authenticated);

        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Authenticated", (string)data!.phase);
    }

    [Fact]
    public async Task SetupConfig_ReflectsErrorMessage()
    {
        _state.SetError("Test error");

        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal("Test error", (string)data!.error);
    }

    [Fact]
    public async Task SetupConfig_IncludesServerPort()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        Assert.Equal(_port, (int)data!.server_port);
    }

    [Fact]
    public async Task SetupConfig_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync("/setup/config",
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

    // --- /setup/qr endpoint ---

    [Fact]
    public async Task SetupQr_WithData_ReturnsPngImage()
    {
        using HttpResponseMessage response = await _client.GetAsync(
            "/setup/qr?data=https://example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(imageBytes.Length > 100, "QR PNG should have substantial content");

        // PNG magic bytes: 137 80 78 71
        Assert.Equal(0x89, imageBytes[0]);
        Assert.Equal(0x50, imageBytes[1]);
        Assert.Equal(0x4E, imageBytes[2]);
        Assert.Equal(0x47, imageBytes[3]);
    }

    [Fact]
    public async Task SetupQr_WithoutData_Returns400()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/qr");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetupQr_PostReturns405()
    {
        using HttpResponseMessage response = await _client.PostAsync(
            "/setup/qr?data=test", new StringContent(""));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // --- /setup/device-code endpoint ---

    [Fact]
    public async Task SetupDeviceCode_GetReturns405()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/device-code");

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

    [Fact]
    public async Task SetupConfig_WithTrailingSlash_ReturnsOk()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/config/");

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

public class SetupServerPkceTests : IAsyncLifetime
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

    [Fact]
    public async Task SetupConfig_ContainsCodeChallenge()
    {
        using HttpResponseMessage response = await _client.GetAsync("/setup/config");
        string body = await response.Content.ReadAsStringAsync();
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(body);

        string codeChallenge = (string)data!.code_challenge;
        Assert.NotNull(codeChallenge);
        Assert.NotEmpty(codeChallenge);
        Assert.True(codeChallenge.Length >= 32, "Code challenge should be at least 32 chars");
    }

    [Fact]
    public async Task SetupConfig_CodeChallengeIsConsistentAcrossRequests()
    {
        using HttpResponseMessage response1 = await _client.GetAsync("/setup/config");
        string body1 = await response1.Content.ReadAsStringAsync();
        dynamic? data1 = JsonConvert.DeserializeObject<dynamic>(body1);

        using HttpResponseMessage response2 = await _client.GetAsync("/setup/config");
        string body2 = await response2.Content.ReadAsStringAsync();
        dynamic? data2 = JsonConvert.DeserializeObject<dynamic>(body2);

        Assert.Equal((string)data1!.code_challenge, (string)data2!.code_challenge);
    }

    [Fact]
    public async Task SsoCallback_WithCode_TransitionsToAuthenticating()
    {
        using HttpResponseMessage response = await _client.GetAsync("/sso-callback?code=test-auth-code");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Authentication received", body);
        Assert.Contains("/setup", body);
    }

    [Fact]
    public async Task SsoCallback_WithCode_ReturnsRedirectHtml()
    {
        using HttpResponseMessage response = await _client.GetAsync("/sso-callback?code=test-code");

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("window.location.href='/setup'", body);
    }
}

public class SetupServerRedirectUriTests
{
    [Fact]
    public void BuildRedirectUri_ConstructsCorrectUri()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("192.168.1.100", 7626);

        string result = SetupServer.BuildRedirectUri(context.Request);

        Assert.Equal("http://192.168.1.100:7626/sso-callback", result);
    }

    [Fact]
    public void BuildRedirectUri_HandlesLocalhostWithPort()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);

        string result = SetupServer.BuildRedirectUri(context.Request);

        Assert.Equal("http://localhost:8080/sso-callback", result);
    }

    [Fact]
    public void BuildRedirectUri_HandlesHostWithoutPort()
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");

        string result = SetupServer.BuildRedirectUri(context.Request);

        Assert.Equal("https://example.com/sso-callback", result);
    }
}

public class SetupServerHtmlTests
{
    [Fact]
    public void LoadSetupHtml_ReturnsValidHtml()
    {
        SetupServer.ClearHtmlCache();
        string html = SetupServer.LoadSetupHtml();

        Assert.NotNull(html);
        Assert.NotEmpty(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void LoadSetupHtml_ContainsSetupUI()
    {
        SetupServer.ClearHtmlCache();
        string html = SetupServer.LoadSetupHtml();

        Assert.Contains("NoMercy", html);
        Assert.Contains("Login with NoMercy", html);
        Assert.Contains("/setup/config", html);
        Assert.Contains("/setup/status", html);
    }

    [Fact]
    public void LoadSetupHtml_UsesServerSidePkce()
    {
        SetupServer.ClearHtmlCache();
        string html = SetupServer.LoadSetupHtml();

        Assert.Contains("config.code_challenge", html);
        Assert.Contains("code_challenge_method=S256", html);
        Assert.Contains("buildAuthUrl()", html);
        Assert.DoesNotContain("generateCodeVerifier", html);
        Assert.DoesNotContain("sessionStorage.setItem", html);
    }

    [Fact]
    public void LoadSetupHtml_CachesResult()
    {
        SetupServer.ClearHtmlCache();
        string first = SetupServer.LoadSetupHtml();
        string second = SetupServer.LoadSetupHtml();

        Assert.Same(first, second);
    }
}

public class SetupServerQrCodeTests
{
    [Fact]
    public void GenerateQrCodePng_ReturnsValidPng()
    {
        byte[] result = SetupServer.GenerateQrCodePng("https://example.com/test");

        Assert.NotNull(result);
        Assert.True(result.Length > 100);

        // PNG magic bytes
        Assert.Equal(0x89, result[0]);
        Assert.Equal(0x50, result[1]);
        Assert.Equal(0x4E, result[2]);
        Assert.Equal(0x47, result[3]);
    }

    [Fact]
    public void GenerateQrCodePng_DifferentDataProducesDifferentOutput()
    {
        byte[] result1 = SetupServer.GenerateQrCodePng("data1");
        byte[] result2 = SetupServer.GenerateQrCodePng("data2");

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void GenerateQrCodePng_HandlesLongUrl()
    {
        string longUrl = "https://auth.nomercy.tv/realms/NoMercyTV/protocol/openid-connect/auth?"
                         + "client_id=nomercy-server&redirect_uri=http://192.168.1.100:7626/sso-callback"
                         + "&response_type=code&scope=openid+offline_access+email+profile";

        byte[] result = SetupServer.GenerateQrCodePng(longUrl);

        Assert.NotNull(result);
        Assert.True(result.Length > 100);
    }
}
