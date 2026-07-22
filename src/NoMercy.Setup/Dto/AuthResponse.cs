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

using Newtonsoft.Json;

namespace NoMercy.Setup.Dto;

public class AuthResponse
{
    [JsonProperty(propertyName: "access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty(propertyName: "expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty(propertyName: "id_token")]
    public string? IdToken { get; set; }

    [JsonProperty(propertyName: "not-before-policy")]
    public int NotBeforePolicy { get; set; }

    [JsonProperty(propertyName: "refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    [JsonProperty(propertyName: "refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty(propertyName: "scope")]
    public string? Scope { get; set; }

    [JsonProperty(propertyName: "session_state")]
    public string? SessionState { get; set; }

    [JsonProperty(propertyName: "token_type")]
    public string? TokenType { get; set; }
}

public class AuthKeysResponse
{
    [JsonProperty(propertyName: "account-service")]
    public string AccountService { get; set; } = string.Empty;

    [JsonProperty(propertyName: "public_key")]
    public string PublicKey { get; set; } = string.Empty;

    [JsonProperty(propertyName: "realm")]
    public string Realm { get; set; } = string.Empty;

    [JsonProperty(propertyName: "token-service")]
    public string TokenService { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tokens-not-before")]
    public int TokensNotBefore { get; set; }
}

public class DeviceAuthResponse
{
    [JsonProperty(propertyName: "device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonProperty(propertyName: "expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty(propertyName: "interval")]
    public int Interval { get; set; }

    [JsonProperty(propertyName: "user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonProperty(propertyName: "verification_uri")]
    public string VerificationUri { get; set; } = string.Empty;

    [JsonProperty(propertyName: "verification_uri_complete")]
    public string VerificationUriComplete { get; set; } = string.Empty;
}
