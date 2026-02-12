using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup;

public static class Auth
{
    private static string? PublicKey { get; set; }
    private static string? RefreshToken { get; set; }
    private static int? ExpiresIn { get; set; }
    private static int? NotBefore { get; set; }

    private static JwtSecurityToken? _jwtSecurityToken;
    private static string? _codeVerifier;

    private static IWebHost? TempServerInstance { get; set; }

    public static async Task Init()
    {
        if (!File.Exists(AppFiles.TokenFile)) await File.WriteAllTextAsync(AppFiles.TokenFile, "{}");

        await AuthKeys();

        Globals.Globals.AccessToken = GetAccessToken();
        RefreshToken = GetRefreshToken();
        ExpiresIn = TokenExpiration();
        NotBefore = TokenNotBefore();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            try
            {
                await TokenByRefreshGrand();
            }
            catch (Exception)
            {
                //
            }
            return;
        }

        JwtSecurityTokenHandler tokenHandler = new();
        _jwtSecurityToken = tokenHandler.ReadJwtToken(Globals.Globals.AccessToken);

        int expiresInDays = _jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days;

        bool expired = expiresInDays < 0;

        if (!expired)
            try
            {
                await TokenByRefreshGrand();
            }
            catch (Exception)
            {
                await TokenByBrowserOrDeviceGrant();
            }
        else
            await TokenByBrowserOrDeviceGrant();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
            throw new("Failed to get tokens");
    }

    private static async Task TokenByBrowserOrDeviceGrant()
    {
        Logger.Auth("Trying to authenticate by browser or device grant", LogEventLevel.Verbose);

        if (IsDesktopEnvironment())
        {
            await TokenByBrowser();
            return;
        }

        await TokenByDeviceGrant();
    }

    private static async Task TokenByDeviceGrant()
    {
        if (Config.TokenClientId == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Authenticating via device grant", LogEventLevel.Verbose);

        List<KeyValuePair<string, string>> deviceCodeBody = BuildDeviceCodeRequestBody(Config.TokenClientId);

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string deviceCodeResponse = await authClient.SendAndReadAsync(HttpMethod.Post,
            "protocol/openid-connect/auth/device", new FormUrlEncodedContent(deviceCodeBody));

        DeviceAuthResponse deviceData = deviceCodeResponse.FromJson<DeviceAuthResponse>()
                                        ?? throw new("Failed to get device code");

        Logger.Auth($"Scan QR code or visit: {deviceData.VerificationUriComplete}");

        ConsoleQrCode.Display(deviceData.VerificationUriComplete);

        List<KeyValuePair<string, string>> tokenBody = BuildDeviceTokenBody(Config.TokenClientId, deviceData.DeviceCode);

        DateTime expiresAt = DateTime.Now.AddSeconds(deviceData.ExpiresIn);
        bool authenticated = false;

        while (DateTime.Now < expiresAt && !authenticated)
        {
            await Task.Delay(deviceData.Interval * 1000);
            try
            {
                using HttpResponseMessage response = await authClient.SendAsync(HttpMethod.Post,
                    "protocol/openid-connect/token", new FormUrlEncodedContent(tokenBody));

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    AuthResponse data = content.FromJson<AuthResponse>()
                                        ?? throw new("Failed to deserialize JSON");
                    SetTokens(data);
                    authenticated = true;
                    Logger.Auth("Device grant authentication successful");
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    dynamic? error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                    if (error?.error.ToString() != "authorization_pending")
                    {
                        Logger.Auth($"Error: {error?.error_description}", LogEventLevel.Error);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Auth($"Error: {ex.Message}", LogEventLevel.Error);
                return;
            }
        }

        if (!authenticated) throw new("Device authorization timed out");
    }

    private static async Task TokenByBrowser()
    {
        if (string.IsNullOrEmpty(Config.AuthBaseUrl) || string.IsNullOrEmpty(Config.TokenClientId))
            throw new ArgumentException("Auth base URL or client ID is not initialized");

        string baseUrl = Config.AuthBaseUrl.TrimEnd('/') + "/protocol/openid-connect/auth";
        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        string scope = "openid offline_access email profile";

        _codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(_codeVerifier);

        Dictionary<string, string> queryParams = new()
        {
            ["client_id"] = Config.TokenClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        string queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        string url = $"{baseUrl}?{queryString}";
        Logger.Setup($"Opening browser for authentication: {url}", LogEventLevel.Verbose);

        TempServerInstance = TempServer.Start();
        await TempServerInstance.StartAsync();

        OpenBrowser(url);

        await WaitForToken();
    }

    private static async Task WaitForToken()
    {
        while (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            await Task.Delay(1000);
        }

        if (TempServerInstance != null)
            await TempServerInstance.StopAsync();
    }

    private static void SetTokens(AuthResponse data)
    {
        using FileStream tmp = File.OpenWrite(AppFiles.TokenFile);
        tmp.SetLength(0);
        tmp.Write(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.Indented)));

        Logger.Auth("Tokens refreshed");

        Globals.Globals.AccessToken = data.AccessToken;
        RefreshToken = data.RefreshToken;
        ExpiresIn = data.ExpiresIn;
        NotBefore = data.NotBeforePolicy;
    }

    internal static void SetTokensFromSetup(AuthResponse data)
    {
        SetTokens(data);
    }

    private static AuthResponse TokenData()
    {
        string fileContents = File.ReadAllText(AppFiles.TokenFile);
        return fileContents.FromJson<AuthResponse>()
               ?? throw new("Failed to deserialize JSON");
    }

    private static string? GetAccessToken()
    {
        AuthResponse data = TokenData();

        return data.AccessToken;
    }

    private static string? GetRefreshToken()
    {
        AuthResponse data = TokenData();
        return data.RefreshToken;
    }

    private static int? TokenExpiration()
    {
        AuthResponse data = TokenData();
        return data.ExpiresIn;
    }

    private static int? TokenNotBefore()
    {
        AuthResponse data = TokenData();
        return data.NotBeforePolicy;
    }

    private static async Task AuthKeys()
    {
        Logger.Auth("Getting auth keys", LogEventLevel.Verbose);

        GenericHttpClient authClient = new();
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Get, Config.AuthBaseUrl);

        AuthKeysResponse data = JsonConvert.DeserializeObject<AuthKeysResponse>(response)
                                ?? throw new("Failed to deserialize JSON");

        PublicKey = data.PublicKey;
    }

    private static async Task TokenByRefreshGrand()
    {
        if (string.IsNullOrEmpty(Config.TokenClientId) ||
            RefreshToken == null || _jwtSecurityToken == null)
            throw new("Auth keys not initialized.");

        Logger.Auth("Refreshing token");

        List<KeyValuePair<string, string>> body = BuildRefreshTokenBody(Config.TokenClientId, RefreshToken);

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "protocol/openid-connect/token",
            new FormUrlEncodedContent(body));

        AuthResponse data = response.FromJson<AuthResponse>()
                            ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
    }

    public static async Task TokenByAuthorizationCode(string code)
    {
        if (string.IsNullOrEmpty(_codeVerifier))
            throw new("PKCE code verifier is missing. Authorization must be initiated via TokenByBrowser first.");

        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        await TokenByAuthorizationCode(code, _codeVerifier, redirectUri);
    }

    public static async Task TokenByAuthorizationCode(string code, string codeVerifier, string redirectUri)
    {
        Logger.Auth("Getting token by authorization code", LogEventLevel.Verbose);
        if (string.IsNullOrEmpty(Config.TokenClientId))
            throw new("Auth keys not initialized.");

        if (string.IsNullOrEmpty(codeVerifier))
            throw new("PKCE code verifier is missing.");

        List<KeyValuePair<string, string>> body = BuildAuthorizationCodeBody(
            Config.TokenClientId, code, redirectUri, codeVerifier);

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "protocol/openid-connect/token",
            new FormUrlEncodedContent(body));

        AuthResponse data = response.FromJson<AuthResponse>()
                            ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url)?.Dispose();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url)?.Dispose();
        else
            throw new("Unsupported OS");
    }

    private static bool IsDesktopEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return true;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;

        if (string.IsNullOrEmpty(Info.GpuVendors.FirstOrDefault())) return false;

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
    }

    internal static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    internal static List<KeyValuePair<string, string>> BuildAuthorizationCodeBody(
        string clientId, string code, string redirectUri, string codeVerifier)
    {
        return
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId),
            new("scope", "openid offline_access email profile"),
            new("redirect_uri", redirectUri),
            new("code", code),
            new("code_verifier", codeVerifier)
        ];
    }

    internal static List<KeyValuePair<string, string>> BuildPasswordGrantBody(
        string clientId, string username, string password, string? otp = "")
    {
        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "password"),
            new("client_id", clientId),
            new("username", username),
            new("password", password)
        ];

        if (!string.IsNullOrEmpty(otp))
            body.Add(new("totp", otp));

        return body;
    }

    internal static List<KeyValuePair<string, string>> BuildRefreshTokenBody(
        string clientId, string refreshToken)
    {
        return
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId),
            new("refresh_token", refreshToken),
            new("scope", "openid offline_access email profile")
        ];
    }

    internal static List<KeyValuePair<string, string>> BuildDeviceCodeRequestBody(string clientId)
    {
        return
        [
            new("client_id", clientId),
            new("scope", "openid offline_access email profile")
        ];
    }

    internal static List<KeyValuePair<string, string>> BuildDeviceTokenBody(
        string clientId, string deviceCode)
    {
        return
        [
            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            new("client_id", clientId),
            new("device_code", deviceCode)
        ];
    }
}
