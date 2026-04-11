using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup;

public class AuthManager
{
    private readonly AppDbContext _appContext;

    private readonly object _authReadyLock = new();
    private TaskCompletionSource _authReadyTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private CancellationTokenSource? _refreshCts;

    public AuthManager(AppDbContext appContext)
    {
        _appContext = appContext;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public Task WaitForAuthReadyAsync(CancellationToken ct)
    {
        lock (_authReadyLock)
        {
            return _authReadyTcs.Task.WaitAsync(ct);
        }
    }

    public async Task<bool> InitializeAsync()
    {
        MigrateLegacyTokenFile();

        string? accessToken = await LoadSecureValue("auth_access_token");
        string? refreshToken = await LoadSecureValue("auth_refresh_token");
        string? metadataJson = await LoadSecureValue("auth_token_metadata");

        if (string.IsNullOrEmpty(accessToken))
        {
            Logger.Auth(
                "No cached token in DB — authentication required through /setup UI",
                LogEventLevel.Information
            );
            return false;
        }

        DateTime expiresAt = ParseExpiresAt(accessToken, metadataJson);
        bool isValid = expiresAt > DateTime.UtcNow.AddMinutes(5);

        if (isValid)
        {
            Globals.Globals.AccessToken = accessToken;
            OfflineJwksCache.LoadCachedPublicKey();
            SignalAuthReady();
            Logger.Auth("Using cached token (still valid)");
            return true;
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            Logger.Auth("Token expired — attempting refresh", LogEventLevel.Information);
            bool refreshed = await TryRefreshToken(refreshToken);
            if (refreshed)
            {
                SignalAuthReady();
                return true;
            }
        }

        Logger.Auth(
            "Token expired and refresh failed — authentication required",
            LogEventLevel.Warning
        );
        return false;
    }

    public async Task StoreTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTime expiresAt,
        string tokenType
    )
    {
        await UpsertSecureValue("auth_access_token", accessToken);

        if (!string.IsNullOrEmpty(refreshToken))
            await UpsertSecureValue("auth_refresh_token", refreshToken);

        TokenMetadata metadata = new()
        {
            ExpiresAt = expiresAt.ToString("O"),
            TokenType = tokenType,
        };
        await UpsertSecureValue("auth_token_metadata", JsonConvert.SerializeObject(metadata));

        Globals.Globals.AccessToken = accessToken;
        SignalAuthReady();

        Logger.Auth("Tokens stored to DB");
    }

    public async Task StoreTokensAsync(AuthResponse tokens)
    {
        string? accessToken = tokens.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
        {
            Logger.Auth("StoreTokensAsync called with null access token", LogEventLevel.Warning);
            return;
        }

        DateTime expiresAt;
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(accessToken);
            expiresAt = jwt.ValidTo;
        }
        catch
        {
            expiresAt = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn > 0 ? tokens.ExpiresIn : 300);
        }

        await StoreTokensAsync(
            accessToken,
            tokens.RefreshToken,
            expiresAt,
            tokens.TokenType ?? "Bearer"
        );
    }

    public async Task RefreshAsync()
    {
        string? refreshToken = await LoadSecureValue("auth_refresh_token");

        if (string.IsNullOrEmpty(refreshToken))
        {
            Logger.Auth("No refresh token in DB — re-auth required", LogEventLevel.Warning);
            Globals.Globals.AccessToken = null;
            ResetAuthReady();
            return;
        }

        bool success = await TryRefreshToken(refreshToken);
        if (!success)
        {
            Logger.Auth("Background refresh failed — clearing access token", LogEventLevel.Warning);
            Globals.Globals.AccessToken = null;
            ResetAuthReady();
        }
    }

    public void ScheduleBackgroundRefresh(CancellationToken ct)
    {
        _refreshCts?.Cancel();
        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancellationToken linked = _refreshCts.Token;

        _ = Task.Run(
            async () =>
            {
                while (!linked.IsCancellationRequested)
                {
                    try
                    {
                        string? accessToken = Globals.Globals.AccessToken;
                        DateTime expiry = DateTime.UtcNow.AddMinutes(5);

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            try
                            {
                                JwtSecurityTokenHandler handler = new();
                                JwtSecurityToken jwt = handler.ReadJwtToken(accessToken);
                                expiry = jwt.ValidTo;
                            }
                            catch
                            {
                                // Fallback to near-immediate refresh
                            }
                        }

                        TimeSpan delay = expiry - DateTime.UtcNow - TimeSpan.FromSeconds(60);
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, linked);

                        if (linked.IsCancellationRequested)
                            break;

                        Logger.Auth("Proactive token refresh", LogEventLevel.Verbose);
                        await RefreshAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Auth(
                            $"Background refresh error: {ex.Message} — retrying in 60s",
                            LogEventLevel.Warning
                        );
                        await Task.Delay(TimeSpan.FromSeconds(60), linked);
                    }
                }
            },
            linked
        );
    }

    // ── Standalone browser PKCE state (for desktop app flow) ────────────────
    private static string? _pendingCodeVerifier;
    private static string? _pendingState;
    private static TaskCompletionSource<bool>? _pkceCompletionSource;

    public static void PreparePkceBrowserFlow(string codeVerifier, string state)
    {
        _pendingCodeVerifier = codeVerifier;
        _pendingState = state;
        _pkceCompletionSource = new TaskCompletionSource<bool>();
    }

    public static Task<bool>? GetPkceBrowserTask() => _pkceCompletionSource?.Task;

    public static async Task<bool> TryCompletePkceFromCallbackAsync(
        string code,
        string state,
        string redirectUri
    )
    {
        if (_pendingCodeVerifier is null || _pendingState is null || _pkceCompletionSource is null)
            return false;

        if (state != _pendingState)
            return false;

        try
        {
            if (string.IsNullOrEmpty(Config.TokenClientId))
                throw new InvalidOperationException("Auth configuration not available");

            List<KeyValuePair<string, string>> body = BuildAuthorizationCodeBody(
                Config.TokenClientId,
                code,
                redirectUri,
                _pendingCodeVerifier
            );

            string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(body)
            );

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Token exchange failed ({(int)response.StatusCode}): {content}"
                );

            AuthResponse? data = Newtonsoft.Json.JsonConvert.DeserializeObject<AuthResponse>(
                content
            );
            if (data?.AccessToken is null)
                throw new InvalidOperationException("Token response missing access_token");

            Globals.Globals.AccessToken = data.AccessToken;
            _pkceCompletionSource.TrySetResult(true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Auth($"PKCE callback failed: {ex.Message}", LogEventLevel.Error);
            _pkceCompletionSource?.TrySetException(ex);
            return false;
        }
        finally
        {
            _pendingCodeVerifier = null;
            _pendingState = null;
            _pkceCompletionSource = null;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void SignalAuthReady()
    {
        lock (_authReadyLock)
        {
            _authReadyTcs.TrySetResult();
        }
    }

    private void ResetAuthReady()
    {
        lock (_authReadyLock)
        {
            if (_authReadyTcs.Task.IsCompleted)
                _authReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private async Task<bool> TryRefreshToken(string refreshToken)
    {
        if (string.IsNullOrEmpty(Config.TokenClientId))
        {
            Logger.Auth("TokenClientId not configured — cannot refresh", LogEventLevel.Warning);
            return false;
        }

        try
        {
            string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

            List<KeyValuePair<string, string>> body = BuildRefreshTokenBody(
                Config.TokenClientId,
                refreshToken
            );

            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(body)
            );

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Logger.Auth(
                    $"Token refresh returned {(int)response.StatusCode}: {errorBody}",
                    LogEventLevel.Warning
                );
                return false;
            }

            string content = await response.Content.ReadAsStringAsync();
            AuthResponse? data = JsonConvert.DeserializeObject<AuthResponse>(content);

            if (data?.AccessToken == null)
            {
                Logger.Auth("Token refresh response missing access_token", LogEventLevel.Warning);
                return false;
            }

            await StoreTokensAsync(data);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Auth($"Token refresh exception: {ex.Message}", LogEventLevel.Warning);
            return false;
        }
    }

    private void MigrateLegacyTokenFile()
    {
#pragma warning disable CS0618
        string tokenFilePath = AppFiles.TokenFile;
#pragma warning restore CS0618

        if (!File.Exists(tokenFilePath))
            return;

        try
        {
            string fileContents = File.ReadAllText(tokenFilePath);
            if (string.IsNullOrWhiteSpace(fileContents) || fileContents.Trim() == "{}")
            {
                SecureDeleteFile(tokenFilePath);
                return;
            }

            AuthResponse? tokenData = JsonConvert.DeserializeObject<AuthResponse>(fileContents);
            if (tokenData?.AccessToken == null)
            {
                SecureDeleteFile(tokenFilePath);
                return;
            }

            // Store synchronously via blocking call during migration
            StoreTokensAsync(tokenData).GetAwaiter().GetResult();
            SecureDeleteFile(tokenFilePath);
            Logger.Auth(
                "Migrated legacy token.json to encrypted DB storage",
                LogEventLevel.Information
            );
        }
        catch (Exception ex)
        {
            Logger.Auth(
                $"Legacy token migration failed: {ex.Message} — file left intact",
                LogEventLevel.Warning
            );
        }
    }

    private async Task<string?> LoadSecureValue(string key)
    {
        Configuration? row = await _appContext
            .Configuration.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key);

        return row?.SecureValue;
    }

    private async Task UpsertSecureValue(string key, string value)
    {
        Configuration? existing = await _appContext.Configuration.FirstOrDefaultAsync(c =>
            c.Key == key
        );

        if (existing is not null)
        {
            existing.SecureValue = value;
            _appContext.Configuration.Update(existing);
        }
        else
        {
            _appContext.Configuration.Add(
                new Configuration
                {
                    Key = key,
                    Value = string.Empty,
                    SecureValue = value,
                }
            );
        }

        await _appContext.SaveChangesAsync();
    }

    private static DateTime ParseExpiresAt(string? accessToken, string? metadataJson)
    {
        // Try metadata first
        if (!string.IsNullOrEmpty(metadataJson))
        {
            try
            {
                TokenMetadata? metadata = JsonConvert.DeserializeObject<TokenMetadata>(
                    metadataJson
                );
                if (
                    metadata?.ExpiresAt is not null
                    && DateTime.TryParse(metadata.ExpiresAt, out DateTime parsedExpiry)
                )
                    return parsedExpiry;
            }
            catch
            {
                // Fall through to JWT
            }
        }

        // Fall back to JWT exp claim
        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                JwtSecurityTokenHandler handler = new();
                JwtSecurityToken jwt = handler.ReadJwtToken(accessToken);
                return jwt.ValidTo;
            }
            catch
            {
                // Fall through to epoch
            }
        }

        return DateTime.MinValue;
    }

    // ── Static PKCE helpers (copied from Auth.cs) ────────────────────────────

    public static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static List<KeyValuePair<string, string>> BuildAuthorizationCodeBody(
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

    public static List<KeyValuePair<string, string>> BuildRefreshTokenBody(
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

    public static List<KeyValuePair<string, string>> BuildDeviceCodeRequestBody(string clientId)
    {
        return [new("client_id", clientId), new("scope", "openid offline_access email profile")];
    }

    public static List<KeyValuePair<string, string>> BuildDeviceTokenBody(
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

    public static void SecureDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            long fileLength = new FileInfo(path).Length;
            if (fileLength > 0)
            {
                using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None
                );
                byte[] zeros = new byte[Math.Min(fileLength, 4096)];
                long remaining = fileLength;
                while (remaining > 0)
                {
                    int chunk = (int)Math.Min(remaining, zeros.Length);
                    stream.Write(zeros, 0, chunk);
                    remaining -= chunk;
                }
                stream.Flush();
            }

            File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.Auth($"SecureDeleteFile failed for {path}: {ex.Message}", LogEventLevel.Warning);
        }
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

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
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
            throw new PlatformNotSupportedException("Unsupported OS for browser launch");
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed class TokenMetadata
    {
        [JsonProperty("expires_at")]
        public string? ExpiresAt { get; set; }

        [JsonProperty("token_type")]
        public string? TokenType { get; set; }
    }
}
