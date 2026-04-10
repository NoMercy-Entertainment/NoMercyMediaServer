using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Builder;
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

    // PKCE fields for setup flow are now managed by SetupServer

    public static async Task Init()
    {
        if (!File.Exists(AppFiles.TokenFile))
            await File.WriteAllTextAsync(AppFiles.TokenFile, "{}");

        await AuthKeys();

        Globals.Globals.AccessToken = GetAccessToken();
        RefreshToken = GetRefreshToken();
        ExpiresIn = TokenExpiration();
        NotBefore = TokenNotBefore();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            // No token exists — skip interactive auth during startup.
            // Authentication should happen through the /setup web UI instead.
            Logger.Auth(
                "No token found — authentication required through /setup UI",
                LogEventLevel.Information
            );
            throw new InvalidOperationException("No authentication token - setup required");
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
                Logger.Auth(
                    "Refresh token rejected — attempting automatic re-authentication",
                    LogEventLevel.Warning
                );
                try
                {
                    await TokenByBrowserOrDeviceGrant();
                    Logger.Auth(
                        "Automatic re-authentication successful",
                        LogEventLevel.Information
                    );
                }
                catch (Exception)
                {
                    Logger.Auth(
                        "Automatic re-authentication failed — manual setup required at /setup",
                        LogEventLevel.Error
                    );
                    throw new InvalidOperationException("Token refresh failed - setup required");
                }
            }
        else
        {
            Logger.Auth(
                "Refresh token rejected — attempting automatic re-authentication",
                LogEventLevel.Warning
            );
            try
            {
                await TokenByBrowserOrDeviceGrant();
                Logger.Auth("Automatic re-authentication successful", LogEventLevel.Information);
            }
            catch (Exception)
            {
                Logger.Auth(
                    "Automatic re-authentication failed — manual setup required at /setup",
                    LogEventLevel.Error
                );
                throw new InvalidOperationException("Token expired - setup required");
            }
        }

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
            throw new("Failed to get tokens");
    }

    public static async Task<bool> InitWithFallback()
    {
        if (!File.Exists(AppFiles.TokenFile))
            await File.WriteAllTextAsync(AppFiles.TokenFile, "{}");

        // Load cached tokens from file
        try
        {
            Globals.Globals.AccessToken = GetAccessToken();
            RefreshToken = GetRefreshToken();
            ExpiresIn = TokenExpiration();
            NotBefore = TokenNotBefore();
        }
        catch
        {
            // Token file may be empty or invalid
        }

        // Load cached auth keys for offline JWT validation
        OfflineJwksCache.LoadCachedPublicKey();

        if (Globals.Globals.AccessToken is null)
        {
            Logger.Auth("No cached token — authentication requires network");
            return false;
        }

        // Check if token is still usable (local check, no network)
        try
        {
            JwtSecurityTokenHandler tokenHandler = new();
            _jwtSecurityToken = tokenHandler.ReadJwtToken(Globals.Globals.AccessToken);

            if (_jwtSecurityToken.ValidTo > DateTime.UtcNow.AddMinutes(5))
            {
                Logger.Auth("Using cached token (still valid)");

                // Try to refresh in background, but don't block
                try
                {
                    await AuthKeys();
                    await TokenByRefreshGrand();
                }
                catch (Exception refreshEx)
                {
                    if (
                        refreshEx.Message.Contains("400")
                        || refreshEx.Message.Contains("Bad Request")
                    )
                        Logger.Auth(
                            $"Background token refresh failed — refresh token rejected by Keycloak ({refreshEx.Message}). Re-authentication will be required after the current token expires.",
                            LogEventLevel.Warning
                        );
                    else
                        Logger.Auth(
                            $"Background token refresh failed — network unavailable ({refreshEx.Message}). Cached token will continue to be used.",
                            LogEventLevel.Warning
                        );
                }

                return true;
            }
        }
        catch
        {
            // Token parsing failed — try refresh
        }

        // Token expired or invalid — try refresh (needs network)
        try
        {
            await AuthKeys();
            await TokenByRefreshGrand();
            return true;
        }
        catch (Exception e)
        {
            Logger.Auth(
                $"Token refresh failed and no valid token is available: {e.Message}",
                LogEventLevel.Error
            );

            Logger.Auth(
                "Refresh token rejected — attempting automatic re-authentication",
                LogEventLevel.Warning
            );
            try
            {
                await TokenByBrowserOrDeviceGrant();
                Logger.Auth("Automatic re-authentication successful", LogEventLevel.Information);
                return true;
            }
            catch (Exception)
            {
                Logger.Auth(
                    "Automatic re-authentication failed — manual setup required at /setup",
                    LogEventLevel.Error
                );

                // Clear the expired token so downstream code doesn't attempt
                // to use it and fail with repeated 401s
                Globals.Globals.AccessToken = null;
                return false;
            }
        }
    }

    private static async Task TokenByBrowserOrDeviceGrant()
    {
        Logger.Auth("Trying to authenticate by browser or device grant", LogEventLevel.Verbose);

        if (IsDesktopEnvironment())
        {
            await TokenByBrowserInteractive();
            return;
        }

        await TokenByDeviceGrant();
    }

    // Standalone PKCE browser flow for desktop/server (non-setup mode)
    private static string? _pendingCodeVerifier;
    private static string? _pendingState;
    private static TaskCompletionSource<bool>? _pkceCompletionSource;

    internal static async Task TokenByBrowserInteractive()
    {
        if (string.IsNullOrEmpty(Config.AuthBaseUrl) || string.IsNullOrEmpty(Config.TokenClientId))
            throw new ArgumentException("Auth base URL or client ID is not initialized");

        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        string state = GenerateCodeVerifier();

        _pendingCodeVerifier = codeVerifier;
        _pendingState = state;
        _pkceCompletionSource = new TaskCompletionSource<bool>();

        string baseUrl = Config.AuthBaseUrl.TrimEnd('/') + "/protocol/openid-connect/auth";
        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        string scope = "openid offline_access email profile";

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = Config.TokenClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };

        string queryString = string.Join(
            "&",
            queryParams.Select(kvp =>
                $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"
            )
        );
        string url = $"{baseUrl}?{queryString}";
        Logger.Setup($"Opening browser for authentication: {url}", LogEventLevel.Verbose);
        OpenBrowser(url);

        // Wait for /sso-callback to complete
        await _pkceCompletionSource.Task;
    }

    // Called by /sso-callback in non-setup mode
    public static async Task<bool> TryCompletePkceFromCallback(
        string code,
        string state,
        string redirectUri
    )
    {
        if (_pendingCodeVerifier == null || _pendingState == null || _pkceCompletionSource == null)
            return false;
        if (state != _pendingState)
            return false;
        try
        {
            await TokenByAuthorizationCode(code, _pendingCodeVerifier, redirectUri);
            _pkceCompletionSource.TrySetResult(true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Auth($"PKCE callback failed: {ex.Message}", LogEventLevel.Error);
            _pkceCompletionSource.TrySetException(ex);
            return false;
        }
        finally
        {
            _pendingCodeVerifier = null;
            _pendingState = null;
            _pkceCompletionSource = null;
        }
    }

    private static async Task TokenByDeviceGrant()
    {
        if (Config.TokenClientId == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Authenticating via device grant", LogEventLevel.Verbose);

        List<KeyValuePair<string, string>> deviceCodeBody = BuildDeviceCodeRequestBody(
            Config.TokenClientId
        );

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string deviceCodeResponse = await authClient.SendAndReadAsync(
            HttpMethod.Post,
            "protocol/openid-connect/auth/device",
            new FormUrlEncodedContent(deviceCodeBody)
        );

        DeviceAuthResponse deviceData =
            deviceCodeResponse.FromJson<DeviceAuthResponse>()
            ?? throw new("Failed to get device code");

        Logger.Auth($"Scan QR code or visit: {deviceData.VerificationUriComplete}");

        ConsoleQrCode.Display(deviceData.VerificationUriComplete);

        List<KeyValuePair<string, string>> tokenBody = BuildDeviceTokenBody(
            Config.TokenClientId,
            deviceData.DeviceCode
        );

        DateTime expiresAt = DateTime.Now.AddSeconds(deviceData.ExpiresIn);
        bool authenticated = false;

        while (DateTime.Now < expiresAt && !authenticated)
        {
            await Task.Delay(deviceData.Interval * 1000);
            try
            {
                using HttpResponseMessage response = await authClient.SendAsync(
                    HttpMethod.Post,
                    "protocol/openid-connect/token",
                    new FormUrlEncodedContent(tokenBody)
                );

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    AuthResponse data =
                        content.FromJson<AuthResponse>() ?? throw new("Failed to deserialize JSON");
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

        if (!authenticated)
            throw new("Device authorization timed out");
    }

    // Called only by SetupServer, which manages PKCE state
    internal static async Task TokenByBrowser(
        string codeVerifier,
        string codeChallenge,
        string state
    )
    {
        if (string.IsNullOrEmpty(Config.AuthBaseUrl) || string.IsNullOrEmpty(Config.TokenClientId))
            throw new ArgumentException("Auth base URL or client ID is not initialized");

        string baseUrl = Config.AuthBaseUrl.TrimEnd('/') + "/protocol/openid-connect/auth";
        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        string scope = "openid offline_access email profile";

        Dictionary<string, string> queryParams = new()
        {
            ["client_id"] = Config.TokenClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };

        string queryString = string.Join(
            "&",
            queryParams.Select(kvp =>
                $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"
            )
        );

        string url = $"{baseUrl}?{queryString}";
        Logger.Setup($"Opening browser for authentication: {url}", LogEventLevel.Verbose);

        OpenBrowser(url);
        // Wait for token to be set by callback
        await WaitForToken();
    }

    private static async Task WaitForToken()
    {
        while (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            await Task.Delay(1000);
        }
    }

    private static void SetTokens(AuthResponse data)
    {
        string tmpPath = AppFiles.TokenFile + ".tmp";
        File.WriteAllText(tmpPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        File.Move(tmpPath, AppFiles.TokenFile, overwrite: true);

        Logger.Auth("Tokens refreshed");

        Globals.Globals.AccessToken = data.AccessToken;
        RefreshToken = data.RefreshToken;
        ExpiresIn = data.ExpiresIn;
        NotBefore = data.NotBeforePolicy;

        if (!string.IsNullOrEmpty(data.AccessToken))
        {
            JwtSecurityTokenHandler tokenHandler = new();
            _jwtSecurityToken = tokenHandler.ReadJwtToken(data.AccessToken);
        }
    }

    internal static void SetTokensFromSetup(AuthResponse data)
    {
        SetTokens(data);
    }

    private static AuthResponse TokenData()
    {
        string fileContents = File.ReadAllText(AppFiles.TokenFile);
        return fileContents.FromJson<AuthResponse>() ?? throw new("Failed to deserialize JSON");
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

        AuthKeysResponse data =
            JsonConvert.DeserializeObject<AuthKeysResponse>(response)
            ?? throw new("Failed to deserialize JSON");

        PublicKey = data.PublicKey;

        if (!string.IsNullOrEmpty(data.PublicKey))
            OfflineJwksCache.CachePublicKey(data.PublicKey);
    }

    public static void ScheduleBackgroundRefresh(CancellationToken cancellationToken = default)
    {
        _ = Task.Run(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        DateTime expiry = _jwtSecurityToken?.ValidTo ?? DateTime.UtcNow;
                        TimeSpan delay = expiry - DateTime.UtcNow - TimeSpan.FromSeconds(60);

                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                            break;

                        Logger.Auth("Proactive token refresh", LogEventLevel.Verbose);
                        await TokenByRefreshGrand();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                        when (e.Message.Contains("400")
                            || e.Message.Contains("401")
                            || e.Message.Contains("Bad Request")
                            || e.Message.Contains("invalid_grant")
                            || e.Message.Contains("Auth keys not initialized")
                        )
                    {
                        Logger.Auth(
                            $"Refresh token rejected by Keycloak — escalating to device/browser grant",
                            LogEventLevel.Warning
                        );
                        try
                        {
                            await TokenByBrowserOrDeviceGrant();
                        }
                        catch (Exception inner)
                        {
                            Logger.Auth(
                                $"Re-authentication failed: {inner.Message} — manual /setup required",
                                LogEventLevel.Error
                            );
                            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Auth(
                            $"Background token refresh failed: {e.Message} — retrying in 60s",
                            LogEventLevel.Warning
                        );
                        await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                    }
                }
            },
            cancellationToken
        );
    }

    private static async Task TokenByRefreshGrand()
    {
        if (
            string.IsNullOrEmpty(Config.TokenClientId)
            || RefreshToken == null
            || _jwtSecurityToken == null
        )
            throw new("Auth keys not initialized.");

        Logger.Auth("Refreshing token");

        List<KeyValuePair<string, string>> body = BuildRefreshTokenBody(
            Config.TokenClientId,
            RefreshToken
        );

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(
            HttpMethod.Post,
            "protocol/openid-connect/token",
            new FormUrlEncodedContent(body)
        );

        AuthResponse data =
            response.FromJson<AuthResponse>() ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
    }

    // Remove overload that used static _codeVerifier

    public static async Task TokenByAuthorizationCode(
        string code,
        string codeVerifier,
        string redirectUri
    )
    {
        Logger.Auth("Getting token by authorization code", LogEventLevel.Verbose);
        if (string.IsNullOrEmpty(Config.TokenClientId))
            throw new("Auth keys not initialized.");

        if (string.IsNullOrEmpty(codeVerifier))
            throw new("PKCE code verifier is missing.");

        List<KeyValuePair<string, string>> body = BuildAuthorizationCodeBody(
            Config.TokenClientId,
            code,
            redirectUri,
            codeVerifier
        );

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(
            HttpMethod.Post,
            "protocol/openid-connect/token",
            new FormUrlEncodedContent(body)
        );

        AuthResponse data =
            response.FromJson<AuthResponse>() ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
    }

    public static void OpenBrowser(string url)
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

    public static bool IsDesktopEnvironment()
    {
        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        )
            return true;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return false;

        if (string.IsNullOrEmpty(Info.GpuVendors.FirstOrDefault()))
            return false;

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
    }

    internal static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // State is now managed by SetupServer for setup PKCE

    internal static List<KeyValuePair<string, string>> BuildAuthorizationCodeBody(
        string clientId,
        string code,
        string redirectUri,
        string codeVerifier
    )
    {
        return
        [
            new("grant_type", "authorization_code"),
            new("client_id", clientId),
            new("scope", "openid offline_access email profile"),
            new("redirect_uri", redirectUri),
            new("code", code),
            new("code_verifier", codeVerifier),
        ];
    }

    internal static List<KeyValuePair<string, string>> BuildPasswordGrantBody(
        string clientId,
        string username,
        string password,
        string? otp = ""
    )
    {
        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "password"),
            new("client_id", clientId),
            new("username", username),
            new("password", password),
        ];

        if (!string.IsNullOrEmpty(otp))
            body.Add(new("totp", otp));

        return body;
    }

    internal static List<KeyValuePair<string, string>> BuildRefreshTokenBody(
        string clientId,
        string refreshToken
    )
    {
        return
        [
            new("grant_type", "refresh_token"),
            new("client_id", clientId),
            new("refresh_token", refreshToken),
            new("scope", "openid offline_access email profile"),
        ];
    }

    internal static List<KeyValuePair<string, string>> BuildDeviceCodeRequestBody(string clientId)
    {
        return [new("client_id", clientId), new("scope", "openid offline_access email profile")];
    }

    internal static List<KeyValuePair<string, string>> BuildDeviceTokenBody(
        string clientId,
        string deviceCode
    )
    {
        return
        [
            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            new("client_id", clientId),
            new("device_code", deviceCode),
        ];
    }
}
