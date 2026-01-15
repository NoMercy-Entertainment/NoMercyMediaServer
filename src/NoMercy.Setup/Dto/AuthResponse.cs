using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class AuthResponse
{
    [JsonProperty("access_token")] public string? AccessToken { get; set; }
    [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    [JsonProperty("id_token")] public string? IdToken { get; set; }
    [JsonProperty("not-before-policy")] public int NotBeforePolicy { get; set; }
    [JsonProperty("refresh_expires_in")] public int RefreshExpiresIn { get; set; }
    [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
    [JsonProperty("scope")] public string? Scope { get; set; }
    [JsonProperty("session_state")] public string? SessionState { get; set; }
    [JsonProperty("token_type")] public string? TokenType { get; set; }
}

public class AuthKeysResponse
{
    [JsonProperty("account-service")] public string AccountService { get; set; } = string.Empty;
    [JsonProperty("public_key")] public string PublicKey { get; set; } = string.Empty;
    [JsonProperty("realm")] public string Realm { get; set; } = string.Empty;
    [JsonProperty("token-service")] public string TokenService { get; set; } = string.Empty;
    [JsonProperty("tokens-not-before")] public int TokensNotBefore { get; set; }
}

public class DeviceAuthResponse
{
    [JsonProperty("device_code")] public string DeviceCode { get; set; } = string.Empty;
    [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
    [JsonProperty("interval")] public int Interval { get; set; }
    [JsonProperty("user_code")] public string UserCode { get; set; } = string.Empty;
    [JsonProperty("verification_uri")] public string VerificationUri { get; set; } = string.Empty;

    [JsonProperty("verification_uri_complete")]
    public string VerificationUriComplete { get; set; } = string.Empty;
}
