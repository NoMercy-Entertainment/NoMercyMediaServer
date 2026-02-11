using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

public class AuthTokenRequestTests
{
    private static Dictionary<string, string> ToDictionary(List<KeyValuePair<string, string>> body)
    {
        return body.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    // --- Authorization Code Body ---

    [Fact]
    public void AuthCodeBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildAuthorizationCodeBody(
            "my-client", "auth-code-123", "http://localhost:7626/sso-callback", "verifier123");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("client_secret", dict.Keys);
    }

    [Fact]
    public void AuthCodeBody_ContainsCodeVerifier()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildAuthorizationCodeBody(
            "my-client", "auth-code-123", "http://localhost:7626/sso-callback", "verifier123");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("verifier123", dict["code_verifier"]);
    }

    [Fact]
    public void AuthCodeBody_ContainsAllRequiredFields()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildAuthorizationCodeBody(
            "my-client", "auth-code-123", "http://localhost:7626/sso-callback", "verifier123");

        Dictionary<string, string> dict = ToDictionary(body);

        Assert.Equal("authorization_code", dict["grant_type"]);
        Assert.Equal("my-client", dict["client_id"]);
        Assert.Contains("openid", dict["scope"]);
        Assert.Equal("http://localhost:7626/sso-callback", dict["redirect_uri"]);
        Assert.Equal("auth-code-123", dict["code"]);
        Assert.Equal("verifier123", dict["code_verifier"]);
    }

    // --- Password Grant Body ---

    [Fact]
    public void PasswordBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildPasswordGrantBody(
            "my-client", "user@test.com", "pass123");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("client_secret", dict.Keys);
    }

    [Fact]
    public void PasswordBody_ContainsCorrectGrantType()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildPasswordGrantBody(
            "my-client", "user@test.com", "pass123");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("password", dict["grant_type"]);
    }

    [Fact]
    public void PasswordBody_IncludesOtp_WhenProvided()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildPasswordGrantBody(
            "my-client", "user@test.com", "pass123", "123456");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("123456", dict["totp"]);
    }

    [Fact]
    public void PasswordBody_ExcludesOtp_WhenEmpty()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildPasswordGrantBody(
            "my-client", "user@test.com", "pass123", "");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("totp", dict.Keys);
    }

    [Fact]
    public void PasswordBody_ExcludesOtp_WhenNull()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildPasswordGrantBody(
            "my-client", "user@test.com", "pass123", null);

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("totp", dict.Keys);
    }

    // --- Refresh Token Body ---

    [Fact]
    public void RefreshBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildRefreshTokenBody(
            "my-client", "refresh-token-abc");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("client_secret", dict.Keys);
    }

    [Fact]
    public void RefreshBody_ContainsRefreshToken()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildRefreshTokenBody(
            "my-client", "refresh-token-abc");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("refresh-token-abc", dict["refresh_token"]);
    }

    [Fact]
    public void RefreshBody_ContainsCorrectGrantType()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildRefreshTokenBody(
            "my-client", "refresh-token-abc");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("refresh_token", dict["grant_type"]);
    }

    // --- Device Code Request Body ---

    [Fact]
    public void DeviceCodeBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildDeviceCodeRequestBody("my-client");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("client_secret", dict.Keys);
    }

    [Fact]
    public void DeviceCodeBody_ContainsClientIdAndScope()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildDeviceCodeRequestBody("my-client");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("my-client", dict["client_id"]);
        Assert.Contains("openid", dict["scope"]);
    }

    // --- Device Token Body ---

    [Fact]
    public void DeviceTokenBody_DoesNotContainClientSecret()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildDeviceTokenBody(
            "my-client", "device-code-xyz");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.DoesNotContain("client_secret", dict.Keys);
    }

    [Fact]
    public void DeviceTokenBody_ContainsDeviceCode()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildDeviceTokenBody(
            "my-client", "device-code-xyz");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("device-code-xyz", dict["device_code"]);
    }

    [Fact]
    public void DeviceTokenBody_ContainsCorrectGrantType()
    {
        List<KeyValuePair<string, string>> body = Auth.BuildDeviceTokenBody(
            "my-client", "device-code-xyz");

        Dictionary<string, string> dict = ToDictionary(body);
        Assert.Equal("urn:ietf:params:oauth:grant-type:device_code", dict["grant_type"]);
    }

    // --- PKCE Code Verifier ---

    [Fact]
    public void CodeVerifier_IsBase64UrlSafe()
    {
        string verifier = Auth.GenerateCodeVerifier();

        Assert.DoesNotContain("+", verifier);
        Assert.DoesNotContain("/", verifier);
        Assert.DoesNotContain("=", verifier);
    }

    [Fact]
    public void CodeVerifier_HasMinLength43()
    {
        string verifier = Auth.GenerateCodeVerifier();

        Assert.True(verifier.Length >= 43, $"Code verifier length {verifier.Length} is less than RFC 7636 minimum of 43");
    }

    [Fact]
    public void CodeVerifier_IsUnique()
    {
        string verifier1 = Auth.GenerateCodeVerifier();
        string verifier2 = Auth.GenerateCodeVerifier();

        Assert.NotEqual(verifier1, verifier2);
    }

    // --- PKCE Code Challenge ---

    [Fact]
    public void CodeChallenge_IsBase64UrlSafe()
    {
        string verifier = Auth.GenerateCodeVerifier();
        string challenge = Auth.GenerateCodeChallenge(verifier);

        Assert.DoesNotContain("+", challenge);
        Assert.DoesNotContain("/", challenge);
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void CodeChallenge_MatchesKnownS256Hash()
    {
        // Known test vector: SHA256("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") base64url-encoded
        // This is from RFC 7636 Appendix B
        string knownVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        string challenge = Auth.GenerateCodeChallenge(knownVerifier);

        Assert.Equal(expectedChallenge, challenge);
    }

    [Fact]
    public void CodeChallenge_DiffersForDifferentVerifiers()
    {
        string challenge1 = Auth.GenerateCodeChallenge("verifier-one-abc");
        string challenge2 = Auth.GenerateCodeChallenge("verifier-two-xyz");

        Assert.NotEqual(challenge1, challenge2);
    }
}
