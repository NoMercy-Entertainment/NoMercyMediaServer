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
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.Setup.Auth;

public class AuthManager
{
    private readonly AppDbContext _appContext;

    // Serialises ALL _appContext access: it is a non-thread-safe EF DbContext, so
    // a read (LoadSecureValue) racing a write (UpsertSecureValue) — e.g. the boot
    // refresh timer reading while a PKCE callback stores tokens — throws "a second
    // operation was started on this DbContext". Every access below takes this.
    private readonly SemaphoreSlim _upsertLock = new(initialCount: 1, maxCount: 1);
    private readonly IStorageDriver _driver;

    private readonly object _authReadyLock = new();
    private TaskCompletionSource _authReadyTcs = new(
        creationOptions: TaskCreationOptions.RunContinuationsAsynchronously
    );
    private CancellationTokenSource? _refreshCts;

    private readonly IAuthTokenStore _authTokenStore;

    public AuthManager(
        AppDbContext appContext,
        IStorageDriver driver,
        IAuthTokenStore authTokenStore
    )
    {
        _authTokenStore = authTokenStore;
        _appContext = appContext;
        _driver = driver;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public Task WaitForAuthReadyAsync(CancellationToken ct)
    {
        lock (_authReadyLock)
        {
            return _authReadyTcs.Task.WaitAsync(cancellationToken: ct);
        }
    }

    public async Task<bool> InitializeAsync()
    {
        MigrateLegacyTokenFile();

        string? accessToken = await LoadSecureValue(key: "auth_access_token");
        string? refreshToken = await LoadSecureValue(key: "auth_refresh_token");
        string? metadataJson = await LoadSecureValue(key: "auth_token_metadata");

        if (string.IsNullOrEmpty(value: accessToken))
        {
            Logger.Auth(message: "No cached token in DB — authentication required through /setup UI");
            return false;
        }

        if (!TokenIssuerMatchesConfiguredRealm(accessToken: accessToken))
        {
            Logger.Auth(
                message: $"Cached token issuer doesn't match configured realm {ExternalServicesConfig.Current.AuthBaseUrl} — discarding and requiring re-auth",
                level: LogEventLevel.Warning
            );
            await UpsertSecureValue(key: "auth_access_token", value: string.Empty);
            await UpsertSecureValue(key: "auth_refresh_token", value: string.Empty);
            await UpsertSecureValue(key: "auth_token_metadata", value: string.Empty);
            return false;
        }

        DateTime expiresAt = ParseExpiresAt(accessToken: accessToken, metadataJson: metadataJson);
        bool isValid = expiresAt > DateTime.UtcNow.AddMinutes(value: 5);

        if (isValid)
        {
            _authTokenStore.SetAccessToken(token: accessToken);
            OfflineJwksCache.LoadCachedPublicKey();
            SignalAuthReady();
            Logger.Auth(message: "Using cached token (still valid)");
            return true;
        }

        if (!string.IsNullOrEmpty(value: refreshToken))
        {
            Logger.Auth(message: "Token expired — attempting refresh with retries");
            int[] delays = [1, 3, 5]; // seconds — a brief Keycloak hiccup at boot
            // must not force a full re-auth when the refresh token is still valid.
            for (int attempt = 0; attempt < delays.Length; attempt++)
            {
                bool refreshed = await TryRefreshToken(refreshToken: refreshToken);
                if (refreshed)
                {
                    SignalAuthReady();
                    return true;
                }

                Logger.Auth(
                    message: $"Refresh attempt {attempt + 1} failed, waiting {delays[attempt]}s before retry..."
                );
                await Task.Delay(delay: TimeSpan.FromSeconds(seconds: delays[attempt]));
            }
        }

        Logger.Auth(
            message: "Token expired and refresh failed — authentication required",
            level: LogEventLevel.Warning
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
        await UpsertSecureValue(key: "auth_access_token", value: accessToken);

        if (!string.IsNullOrEmpty(value: refreshToken))
            await UpsertSecureValue(key: "auth_refresh_token", value: refreshToken);

        TokenMetadata metadata = new()
        {
            ExpiresAt = expiresAt.ToString(format: "O"),
            TokenType = tokenType,
        };
        await UpsertSecureValue(key: "auth_token_metadata", value: JsonConvert.SerializeObject(value: metadata));

        _authTokenStore.SetAccessToken(token: accessToken);
        SignalAuthReady();

        Logger.Auth(message: "Tokens stored to DB");
    }

    public async Task StoreTokensAsync(AuthResponse tokens)
    {
        string? accessToken = tokens.AccessToken;
        if (string.IsNullOrEmpty(value: accessToken))
        {
            Logger.Auth(message: "StoreTokensAsync called with null access token", level: LogEventLevel.Warning);
            return;
        }

        DateTime expiresAt;
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            expiresAt = jwt.ValidTo;
        }
        catch
        {
            expiresAt = DateTime.UtcNow.AddSeconds(value: tokens.ExpiresIn > 0 ? tokens.ExpiresIn : 300);
        }

        await StoreTokensAsync(
            accessToken: accessToken,
            refreshToken: tokens.RefreshToken,
            expiresAt: expiresAt,
            tokenType: tokens.TokenType ?? "Bearer"
        );
    }

    public async Task RefreshAsync()
    {
        string? refreshToken = await LoadSecureValue(key: "auth_refresh_token");

        if (string.IsNullOrEmpty(value: refreshToken))
        {
            Logger.Auth(message: "No refresh token in DB — re-auth required", level: LogEventLevel.Warning);
            _authTokenStore.SetAccessToken(token: null);
            ResetAuthReady();
            return;
        }

        bool success = await TryRefreshToken(refreshToken: refreshToken);
        if (!success)
        {
            Logger.Auth(message: "Background refresh failed — clearing access token", level: LogEventLevel.Warning);
            _authTokenStore.SetAccessToken(token: null);
            ResetAuthReady();
        }
    }

    private async Task HandleDeadRefreshTokenAsync()
    {
        _refreshCts?.Cancel();
        await UpsertSecureValue(key: "auth_refresh_token", value: string.Empty);
        _authTokenStore.SetAccessToken(token: null);
        ResetAuthReady();
        Logger.Auth(
            message: "Refresh token rejected as invalid_grant — re-authentication required through /setup UI",
            level: LogEventLevel.Warning
        );
    }

    public void ScheduleBackgroundRefresh(CancellationToken ct)
    {
        _refreshCts?.Cancel();
        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(token: ct);
        CancellationToken linked = _refreshCts.Token;

        _ = Task.Run(
            function: async () =>
            {
                while (!linked.IsCancellationRequested)
                {
                    try
                    {
                        string? accessToken = _authTokenStore.AccessToken;
                        DateTime expiry = DateTime.UtcNow.AddMinutes(value: 5);

                        if (!string.IsNullOrEmpty(value: accessToken))
                        {
                            try
                            {
                                JwtSecurityTokenHandler handler = new();
                                JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
                                expiry = jwt.ValidTo;
                            }
                            catch
                            {
                                // Fallback to near-immediate refresh
                            }
                        }

                        TimeSpan delay = expiry - DateTime.UtcNow - TimeSpan.FromSeconds(seconds: 60);
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay: delay, cancellationToken: linked);

                        if (linked.IsCancellationRequested)
                            break;

                        Logger.Auth(message: "Proactive token refresh", level: LogEventLevel.Verbose);
                        await RefreshAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Auth(
                            message: $"Background refresh error: {ex.Message} — retrying in 60s",
                            level: LogEventLevel.Warning
                        );
                        await Task.Delay(delay: TimeSpan.FromSeconds(seconds: 60), cancellationToken: linked);
                    }
                }
            },
            cancellationToken: linked
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
        _pkceCompletionSource = new();
    }

    public static Task<bool>? GetPkceBrowserTask() => _pkceCompletionSource?.Task;

    public static async Task<bool> TryCompletePkceFromCallbackAsync(
        string code,
        string state,
        string redirectUri,
        IAuthTokenStore? authTokenStore = null
    )
    {
        if (_pendingCodeVerifier is null || _pendingState is null || _pkceCompletionSource is null)
            return false;

        if (state != _pendingState)
            return false;

        try
        {
            if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
                throw new InvalidOperationException(message: "Auth configuration not available");

            List<KeyValuePair<string, string>> body = BuildAuthorizationCodeBody(
                clientId: ExternalServicesConfig.Current.TokenClientId,
                code: code,
                redirectUri: redirectUri,
                codeVerifier: _pendingCodeVerifier
            );

            string tokenEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";

            using HttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage response = await httpClient.PostAsync(
                requestUri: tokenEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: body)
            );

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    message: $"Token exchange failed ({(int)response.StatusCode}): {content}"
                );

            AuthResponse? data = JsonConvert.DeserializeObject<AuthResponse>(value: content);
            if (data?.AccessToken is null)
                throw new InvalidOperationException(message: "Token response missing access_token");

            authTokenStore?.SetAccessToken(token: data.AccessToken);
            _pkceCompletionSource.TrySetResult(result: true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Auth(message: $"PKCE callback failed: {ex.Message}", level: LogEventLevel.Error);
            _pkceCompletionSource?.TrySetException(exception: ex);
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
                _authReadyTcs = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private async Task<bool> TryRefreshToken(string refreshToken)
    {
        if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
        {
            Logger.Auth(message: "TokenClientId not configured — cannot refresh", level: LogEventLevel.Warning);
            return false;
        }

        try
        {
            string tokenEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";

            List<KeyValuePair<string, string>> body = BuildRefreshTokenBody(
                clientId: ExternalServicesConfig.Current.TokenClientId,
                refreshToken: refreshToken
            );

            using HttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage response = await httpClient.PostAsync(
                requestUri: tokenEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: body)
            );

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                Logger.Auth(
                    message: $"Token refresh returned {(int)response.StatusCode}: {errorBody}",
                    level: LogEventLevel.Warning
                );

                if (IsPermanentRefreshFailure(errorBody: errorBody))
                    await HandleDeadRefreshTokenAsync();

                return false;
            }

            string content = await response.Content.ReadAsStringAsync();
            AuthResponse? data = JsonConvert.DeserializeObject<AuthResponse>(value: content);

            if (data?.AccessToken == null)
            {
                Logger.Auth(message: "Token refresh response missing access_token", level: LogEventLevel.Warning);
                return false;
            }

            await StoreTokensAsync(tokens: data);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Auth(message: $"Token refresh exception: {ex.Message}", level: LogEventLevel.Warning);
            return false;
        }
    }

    private static bool TokenIssuerMatchesConfiguredRealm(string accessToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            string issuer = (jwt.Issuer ?? string.Empty).TrimEnd(trimChar: '/');
            string configured = ExternalServicesConfig.Current.AuthBaseUrl.TrimEnd(trimChar: '/');
            return issuer.Equals(value: configured, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void MigrateLegacyTokenFile()
    {
#pragma warning disable CS0618
        string tokenFilePath = AppFiles.TokenFile;
#pragma warning restore CS0618

        if (!_driver.FileExists(path: tokenFilePath))
            return;

        try
        {
            string fileContents;
            using (StreamReader reader = new(stream: _driver.OpenRead(path: tokenFilePath)))
                fileContents = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(value: fileContents) || fileContents.Trim() == "{}")
            {
                SecureDeleteFile(path: tokenFilePath);
                return;
            }

            AuthResponse? tokenData = JsonConvert.DeserializeObject<AuthResponse>(value: fileContents);
            if (tokenData?.AccessToken == null)
            {
                SecureDeleteFile(path: tokenFilePath);
                return;
            }

            // Store synchronously via blocking call during migration
            StoreTokensAsync(tokens: tokenData).GetAwaiter().GetResult();
            SecureDeleteFile(path: tokenFilePath);
            Logger.Auth(message: "Migrated legacy token.json to encrypted DB storage");
        }
        catch (Exception ex)
        {
            Logger.Auth(
                message: $"Legacy token migration failed: {ex.Message} — file left intact",
                level: LogEventLevel.Warning
            );
        }
    }

    private async Task<string?> LoadSecureValue(string key)
    {
        await _upsertLock.WaitAsync();
        try
        {
            Configuration? row = await _appContext
                .Configuration.AsNoTracking()
                .FirstOrDefaultAsync(predicate: c => c.Key == key);

            return row?.SecureValue;
        }
        finally
        {
            _upsertLock.Release();
        }
    }

    private async Task UpsertSecureValue(string key, string value)
    {
        await _upsertLock.WaitAsync();
        try
        {
            Configuration? existing = await _appContext.Configuration.FirstOrDefaultAsync(predicate: c =>
                c.Key == key
            );

            if (existing is not null)
            {
                existing.SecureValue = value;
                _appContext.Configuration.Update(entity: existing);
            }
            else
            {
                _appContext.Configuration.Add(
                    entity: new()
                    {
                        Key = key,
                        Value = string.Empty,
                        SecureValue = value,
                    }
                );
            }

            await _appContext.SaveChangesAsync();
        }
        finally
        {
            _upsertLock.Release();
        }
    }

    private static DateTime ParseExpiresAt(string? accessToken, string? metadataJson)
    {
        // Try metadata first
        if (!string.IsNullOrEmpty(value: metadataJson))
        {
            try
            {
                TokenMetadata? metadata = JsonConvert.DeserializeObject<TokenMetadata>(
                    value: metadataJson
                );
                if (
                    metadata?.ExpiresAt is not null
                    && DateTime.TryParse(s: metadata.ExpiresAt, result: out DateTime parsedExpiry)
                )
                    return parsedExpiry;
            }
            catch
            {
                // Fall through to JWT
            }
        }

        // Fall back to JWT exp claim
        if (!string.IsNullOrEmpty(value: accessToken))
        {
            try
            {
                JwtSecurityTokenHandler handler = new();
                JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
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
        RandomNumberGenerator.Fill(data: bytes);
        return Convert.ToBase64String(inArray: bytes).TrimEnd(trimChar: '=').Replace(oldChar: '+', newChar: '-').Replace(oldChar: '/', newChar: '_');
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(source: Encoding.ASCII.GetBytes(s: codeVerifier));
        return Convert.ToBase64String(inArray: hash).TrimEnd(trimChar: '=').Replace(oldChar: '+', newChar: '-').Replace(oldChar: '/', newChar: '_');
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
            new(key: "grant_type", value: "authorization_code"),
            new(key: "client_id", value: clientId),
            new(key: "scope", value: "openid offline_access email profile"),
            new(key: "redirect_uri", value: redirectUri),
            new(key: "code", value: code),
            new(key: "code_verifier", value: codeVerifier),
        ];
    }

    public static bool IsPermanentRefreshFailure(string errorBody) =>
        errorBody.Contains(value: "invalid_grant", comparisonType: StringComparison.OrdinalIgnoreCase);

    public static List<KeyValuePair<string, string>> BuildRefreshTokenBody(
        string clientId,
        string refreshToken
    )
    {
        return
        [
            new(key: "grant_type", value: "refresh_token"),
            new(key: "client_id", value: clientId),
            new(key: "refresh_token", value: refreshToken),
            new(key: "scope", value: "openid offline_access email profile"),
        ];
    }

    public static List<KeyValuePair<string, string>> BuildDeviceCodeRequestBody(string clientId)
    {
        return [new(key: "client_id", value: clientId), new(key: "scope", value: "openid offline_access email profile")];
    }

    public static List<KeyValuePair<string, string>> BuildDeviceTokenBody(
        string clientId,
        string deviceCode
    )
    {
        return
        [
            new(key: "grant_type", value: "urn:ietf:params:oauth:grant-type:device_code"),
            new(key: "client_id", value: clientId),
            new(key: "device_code", value: deviceCode),
        ];
    }

    public void SecureDeleteFile(string path)
    {
        try
        {
            if (!_driver.FileExists(path: path))
                return;

            long fileLength = _driver.GetFileSize(path: path);
            if (fileLength > 0)
            {
                using Stream stream = new FileStream(
                    path: path,
                    mode: FileMode.Open,
                    access: FileAccess.Write,
                    share: FileShare.None
                );
                byte[] zeros = new byte[Math.Min(val1: fileLength, val2: 4096)];
                long remaining = fileLength;
                while (remaining > 0)
                {
                    int chunk = (int)Math.Min(val1: remaining, val2: zeros.Length);
                    stream.Write(buffer: zeros, offset: 0, count: chunk);
                    remaining -= chunk;
                }
                stream.Flush();
            }

            _driver.DeleteFile(path: path);
        }
        catch (Exception ex)
        {
            Logger.Auth(message: $"SecureDeleteFile failed for {path}: {ex.Message}", level: LogEventLevel.Warning);
        }
    }

    public static bool IsDesktopEnvironment()
    {
        if (
            RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows)
            || RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX)
        )
            return true;

        if (!RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            return false;

        return !string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "DISPLAY"))
            || !string.IsNullOrEmpty(value: Environment.GetEnvironmentVariable(variable: "WAYLAND_DISPLAY"));
    }

    public static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Windows))
            Process.Start(startInfo: new ProcessStartInfo(fileName: url) { UseShellExecute = true })?.Dispose();
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.Linux))
            Process.Start(fileName: "xdg-open", arguments: url).Dispose();
        else if (RuntimeInformation.IsOSPlatform(osPlatform: OSPlatform.OSX))
            Process.Start(fileName: "open", arguments: url).Dispose();
        else
            throw new PlatformNotSupportedException(message: "Unsupported OS for browser launch");
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed class TokenMetadata
    {
        [JsonProperty(propertyName: "expires_at")]
        public string? ExpiresAt { get; set; }

        [JsonProperty(propertyName: "token_type")]
        public string? TokenType { get; set; }
    }
}
