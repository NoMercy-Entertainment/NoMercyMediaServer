using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup.Cast;

/// <summary>
/// Mints LaunchCustomData bundles for cast sessions. Calls Keycloak's
/// token-exchange grant against the nomercy-cast-receiver client to issue
/// audience-scoped access + refresh tokens.
///
/// The server's own access token (Globals.Globals.AccessToken) is the subject;
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
public class CastSessionTokenService
{
    private const string CastReceiverClientId = "nomercy-cast-receiver";
    private const string CastReceiverAudience = "cast_receiver";
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
        if (string.IsNullOrEmpty(Globals.Globals.AccessToken))
        {
            Logger.Auth(
                "CastSessionTokenService: server access token not available — cannot mint cast bundle",
                LogEventLevel.Warning
            );
            return null;
        }

        AuthResponse? exchanged = await RequestTokenExchangeAsync(userId);
        if (exchanged?.AccessToken is null || string.IsNullOrEmpty(exchanged.RefreshToken))
        {
            Logger.Auth(
                "CastSessionTokenService: token-exchange returned no usable tokens",
                LogEventLevel.Warning
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
        string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", TokenExchangeGrantType),
            new("client_id", CastReceiverClientId),
            new("subject_token", Globals.Globals.AccessToken!),
            new("subject_token_type", AccessTokenType),
            new("requested_subject", userId.ToString()),
            new("audience", CastReceiverAudience),
            new("requested_token_type", RefreshTokenType),
            new("scope", "openid"),
        ];

        try
        {
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage response = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(body)
            );

            string content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Auth(
                    $"Cast token-exchange failed ({(int)response.StatusCode}): {content}",
                    LogEventLevel.Warning
                );
                return null;
            }

            return JsonConvert.DeserializeObject<AuthResponse>(content);
        }
        catch (Exception ex)
        {
            Logger.Auth($"Cast token-exchange exception: {ex.Message}", LogEventLevel.Warning);
            return null;
        }
    }
}
