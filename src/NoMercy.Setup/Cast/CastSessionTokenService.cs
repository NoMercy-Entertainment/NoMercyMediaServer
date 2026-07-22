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

using System.IdentityModel.Tokens.Jwt;
using NoMercy.NmSystem.Auth;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup.Cast;

/// <summary>
/// Mints LaunchCustomData bundles for cast sessions. Calls Keycloak's
/// token-exchange grant against the nomercy-cast-receiver client to issue
/// audience-scoped access + refresh tokens.
///
/// The server's own access token (authTokenStore.AccessToken) is the subject;
/// requested_subject names the target user. This requires the Keycloak realm
/// to grant the server's auth client token-exchange permission to
/// nomercy-cast-receiver — configured once via the admin console after
/// importing cast-receiver-client.json.
///
/// Per-session metadata (device_id, server_id, cast_session_id, intent) rides
/// on the customData JSON alongside the tokens. Standard Keycloak
/// token-exchange does not support injecting custom session-bound claims
/// into the issued JWT without a custom SPI; v1 keeps the receiver's
/// session metadata in customData fields and uses the JWT for audience +
/// subject only.
/// </summary>
public class CastSessionTokenService(AuthManager authManager, IAuthTokenStore authTokenStore)
{
    private const string CastReceiverClientId = "nomercy-cast-receiver";
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";
    private const string RefreshTokenType = "urn:ietf:params:oauth:token-type:refresh_token";

    public async Task<LaunchCustomData?> MintAsync(
        Guid userId,
        string serverId,
        string serverUrl,
        Ulid deviceId,
        CastIntent intent,
        string clientLocale = "en-US"
    )
    {
        if (string.IsNullOrEmpty(value: authTokenStore.AccessToken))
        {
            Logger.Auth(
                message: "CastSessionTokenService: server access token not available — cannot mint cast bundle",
                level: LogEventLevel.Warning
            );
            return null;
        }

        await authManager.RefreshAsync();

        if (string.IsNullOrEmpty(value: authTokenStore.AccessToken))
        {
            Logger.Auth(
                message: "CastSessionTokenService: refresh dropped the access token — cannot mint cast bundle",
                level: LogEventLevel.Warning
            );
            return null;
        }

        AuthResponse? exchanged = await RequestTokenExchangeAsync(userId: userId);
        if (exchanged?.AccessToken is null || string.IsNullOrEmpty(value: exchanged.RefreshToken))
        {
            Logger.Auth(
                message: "CastSessionTokenService: token-exchange returned no usable tokens",
                level: LogEventLevel.Warning
            );
            return null;
        }

        string castSessionId = Guid.NewGuid().ToString();

        return new()
        {
            AccessToken = exchanged.AccessToken,
            RefreshToken = exchanged.RefreshToken,
            UserId = userId.ToString(),
            ServerId = serverId,
            ServerUrl = serverUrl,
            DeviceId = deviceId.ToString(),
            Intent = intent,
            CastSessionId = castSessionId,
            LaunchTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClientLocale = clientLocale,
        };
    }

    private async Task<AuthResponse?> RequestTokenExchangeAsync(Guid userId)
    {
        string tokenEndpoint = $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";
        string requestingClientId = ResolveRequestingClientId(accessToken: authTokenStore.AccessToken!);

        if (!IssuerMatchesConfiguredRealm(accessToken: authTokenStore.AccessToken!))
        {
            Logger.Auth(
                message: $"CastSessionTokenService: subject token issuer doesn't match configured realm {ExternalServicesConfig.Current.AuthBaseUrl} — re-auth required against the active realm before cast tokens can be minted",
                level: LogEventLevel.Warning
            );
            return null;
        }

        List<KeyValuePair<string, string>> body =
        [
            new(key: "grant_type", value: TokenExchangeGrantType),
            new(key: "client_id", value: requestingClientId),
            new(key: "subject_token", value: authTokenStore.AccessToken!),
            new(key: "subject_token_type", value: AccessTokenType),
            new(key: "audience", value: CastReceiverClientId),
            new(key: "requested_token_type", value: RefreshTokenType),
            new(key: "scope", value: "openid"),
        ];

        try
        {
            using HttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage response = await httpClient.PostAsync(
                requestUri: tokenEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: body)
            );

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string subjectInfo = DescribeSubjectToken(accessToken: authTokenStore.AccessToken!);
                Logger.Auth(
                    message: $"Cast token-exchange failed ({(int)response.StatusCode}): {content} | endpoint: {tokenEndpoint} | subject: {subjectInfo} | requesting_client: {requestingClientId} target_audience: {CastReceiverClientId}",
                    level: LogEventLevel.Warning
                );
                return null;
            }

            return JsonConvert.DeserializeObject<AuthResponse>(value: content);
        }
        catch (Exception ex)
        {
            Logger.Auth(message: $"Cast token-exchange exception: {ex.Message}", level: LogEventLevel.Warning);
            return null;
        }
    }

    private static string ResolveRequestingClientId(string accessToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            if (
                jwt.Payload.TryGetValue(key: "azp", value: out object? azp)
                && azp is string s
                && !string.IsNullOrEmpty(value: s)
            )
                return s;
        }
        catch
        {
            // fall through
        }
        return CastReceiverClientId;
    }

    private static bool IssuerMatchesConfiguredRealm(string accessToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            string issuer = jwt.Issuer ?? string.Empty;
            string configured = ExternalServicesConfig.Current.AuthBaseUrl.TrimEnd(trimChar: '/');
            return issuer.TrimEnd(trimChar: '/').Equals(value: configured, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool NeedsRefresh(string accessToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            return jwt.ValidTo <= DateTime.UtcNow.AddSeconds(value: 60);
        }
        catch
        {
            return true;
        }
    }

    private static string DescribeSubjectToken(string accessToken)
    {
        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(token: accessToken);
            string issuer = jwt.Issuer ?? "?";
            string azp = jwt.Payload.TryGetValue(key: "azp", value: out object? a) ? a?.ToString() ?? "?" : "?";
            string aud = string.Join(separator: ",", values: jwt.Audiences);
            string sub = jwt.Subject ?? "?";
            DateTime exp = jwt.ValidTo;
            TimeSpan remaining = exp - DateTime.UtcNow;
            return $"iss={issuer} azp={azp} aud=[{aud}] sub={sub} exp={exp:O} remaining={remaining.TotalSeconds:F0}s";
        }
        catch (Exception ex)
        {
            return $"unparseable JWT: {ex.Message}";
        }
    }
}
