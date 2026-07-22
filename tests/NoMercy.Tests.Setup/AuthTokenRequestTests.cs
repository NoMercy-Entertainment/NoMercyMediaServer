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

using NoMercy.Setup.Auth;

namespace NoMercy.Tests.Setup;

public class AuthTokenRequestTests
{
    private static Dictionary<string, string> ToDictionary(List<KeyValuePair<string, string>> body)
    {
        return body.ToDictionary(keySelector: kvp => kvp.Key, elementSelector: kvp => kvp.Value);
    }

    // --- Authorization Code Body ---

    [Fact]
    public void AuthCodeBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildAuthorizationCodeBody(
            clientId: "my-client",
            code: "auth-code-123",
            redirectUri: "http://localhost:7626/sso-callback",
            codeVerifier: "verifier123"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.DoesNotContain(expected: "client_secret", collection: dict.Keys);
    }

    [Fact]
    public void AuthCodeBody_ContainsCodeVerifier()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildAuthorizationCodeBody(
            clientId: "my-client",
            code: "auth-code-123",
            redirectUri: "http://localhost:7626/sso-callback",
            codeVerifier: "verifier123"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "verifier123", actual: dict[key: "code_verifier"]);
    }

    [Fact]
    public void AuthCodeBody_ContainsAllRequiredFields()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildAuthorizationCodeBody(
            clientId: "my-client",
            code: "auth-code-123",
            redirectUri: "http://localhost:7626/sso-callback",
            codeVerifier: "verifier123"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);

        Assert.Equal(expected: "authorization_code", actual: dict[key: "grant_type"]);
        Assert.Equal(expected: "my-client", actual: dict[key: "client_id"]);
        Assert.Contains(expectedSubstring: "openid", actualString: dict[key: "scope"]);
        Assert.Equal(expected: "http://localhost:7626/sso-callback", actual: dict[key: "redirect_uri"]);
        Assert.Equal(expected: "auth-code-123", actual: dict[key: "code"]);
        Assert.Equal(expected: "verifier123", actual: dict[key: "code_verifier"]);
    }

    // --- Refresh Token Body ---

    [Fact]
    public void RefreshBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildRefreshTokenBody(
            clientId: "my-client",
            refreshToken: "refresh-token-abc"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.DoesNotContain(expected: "client_secret", collection: dict.Keys);
    }

    [Fact]
    public void RefreshBody_ContainsRefreshToken()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildRefreshTokenBody(
            clientId: "my-client",
            refreshToken: "refresh-token-abc"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "refresh-token-abc", actual: dict[key: "refresh_token"]);
    }

    [Fact]
    public void RefreshBody_ContainsCorrectGrantType()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildRefreshTokenBody(
            clientId: "my-client",
            refreshToken: "refresh-token-abc"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "refresh_token", actual: dict[key: "grant_type"]);
    }

    // --- Permanent Refresh Failure Detection ---

    [Fact]
    public void PermanentRefreshFailure_TrueForInvalidGrant()
    {
        string body =
            "{\"error\":\"invalid_grant\",\"error_description\":\"Offline user session not found\"}";

        Assert.True(condition: AuthManager.IsPermanentRefreshFailure(errorBody: body));
    }

    [Fact]
    public void PermanentRefreshFailure_IsCaseInsensitive()
    {
        Assert.True(condition: AuthManager.IsPermanentRefreshFailure(errorBody: "ERROR: INVALID_GRANT"));
    }

    [Fact]
    public void PermanentRefreshFailure_FalseForTransientErrors()
    {
        Assert.False(
            condition: AuthManager.IsPermanentRefreshFailure(errorBody: "{\"error\":\"temporarily_unavailable\"}")
        );
        Assert.False(condition: AuthManager.IsPermanentRefreshFailure(errorBody: "502 Bad Gateway"));
        Assert.False(condition: AuthManager.IsPermanentRefreshFailure(errorBody: string.Empty));
    }

    // --- Device Code Request Body ---

    [Fact]
    public void DeviceCodeBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceCodeRequestBody(
            clientId: "my-client"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.DoesNotContain(expected: "client_secret", collection: dict.Keys);
    }

    [Fact]
    public void DeviceCodeBody_ContainsClientIdAndScope()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceCodeRequestBody(
            clientId: "my-client"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "my-client", actual: dict[key: "client_id"]);
        Assert.Contains(expectedSubstring: "openid", actualString: dict[key: "scope"]);
    }

    // --- Device Token Body ---

    [Fact]
    public void DeviceTokenBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceTokenBody(
            clientId: "my-client",
            deviceCode: "device-code-xyz"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.DoesNotContain(expected: "client_secret", collection: dict.Keys);
    }

    [Fact]
    public void DeviceTokenBody_ContainsDeviceCode()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceTokenBody(
            clientId: "my-client",
            deviceCode: "device-code-xyz"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "device-code-xyz", actual: dict[key: "device_code"]);
    }

    [Fact]
    public void DeviceTokenBody_ContainsCorrectGrantType()
    {
        List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceTokenBody(
            clientId: "my-client",
            deviceCode: "device-code-xyz"
        );

        Dictionary<string, string> dict = ToDictionary(body: body);
        Assert.Equal(expected: "urn:ietf:params:oauth:grant-type:device_code", actual: dict[key: "grant_type"]);
    }

    // --- PKCE Code Verifier ---

    [Fact]
    public void CodeVerifier_IsBase64UrlSafe()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        Assert.DoesNotContain(expectedSubstring: "+", actualString: verifier);
        Assert.DoesNotContain(expectedSubstring: "/", actualString: verifier);
        Assert.DoesNotContain(expectedSubstring: "=", actualString: verifier);
    }

    [Fact]
    public void CodeVerifier_HasMinLength43()
    {
        string verifier = AuthManager.GenerateCodeVerifier();

        Assert.True(
            condition: verifier.Length >= 43,
            userMessage: $"Code verifier length {verifier.Length} is less than RFC 7636 minimum of 43"
        );
    }

    [Fact]
    public void CodeVerifier_IsUnique()
    {
        string verifier1 = AuthManager.GenerateCodeVerifier();
        string verifier2 = AuthManager.GenerateCodeVerifier();

        Assert.NotEqual(expected: verifier1, actual: verifier2);
    }

    // --- PKCE Code Challenge ---

    [Fact]
    public void CodeChallenge_IsBase64UrlSafe()
    {
        string verifier = AuthManager.GenerateCodeVerifier();
        string challenge = AuthManager.GenerateCodeChallenge(codeVerifier: verifier);

        Assert.DoesNotContain(expectedSubstring: "+", actualString: challenge);
        Assert.DoesNotContain(expectedSubstring: "/", actualString: challenge);
        Assert.DoesNotContain(expectedSubstring: "=", actualString: challenge);
    }

    [Fact]
    public void CodeChallenge_MatchesKnownS256Hash()
    {
        // Known test vector: SHA256("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") base64url-encoded
        // This is from RFC 7636 Appendix B
        string knownVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        string challenge = AuthManager.GenerateCodeChallenge(codeVerifier: knownVerifier);

        Assert.Equal(expected: expectedChallenge, actual: challenge);
    }

    [Fact]
    public void CodeChallenge_DiffersForDifferentVerifiers()
    {
        string challenge1 = AuthManager.GenerateCodeChallenge(codeVerifier: "verifier-one-abc");
        string challenge2 = AuthManager.GenerateCodeChallenge(codeVerifier: "verifier-two-xyz");

        Assert.NotEqual(expected: challenge1, actual: challenge2);
    }
}
