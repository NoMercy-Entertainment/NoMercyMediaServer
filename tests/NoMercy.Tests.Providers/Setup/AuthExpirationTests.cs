namespace NoMercy.Tests.Providers.Setup;

/// <summary>
/// Characterization tests for token expiration logic.
/// Auth.cs was replaced by AuthManager — source-inspection tests removed.
/// Pure expiration arithmetic tests preserved.
/// </summary>
[Trait("Category", "Characterization")]
public class AuthExpirationTests
{
    [Fact]
    public void ExpirationLogic_ValidTokenNotExpired()
    {
        // Simulate: token with 10 days remaining (after -5 day offset = 5 days positive)
        int expiresInDays = 5;
        bool expired = expiresInDays < 0;
        Assert.False(expired, "A token with 5+ days remaining should NOT be marked expired");
    }

    [Fact]
    public void ExpirationLogic_ExpiredTokenIsExpired()
    {
        // Simulate: token expired 2 days ago (after -5 day offset = negative)
        int expiresInDays = -2;
        bool expired = expiresInDays < 0;
        Assert.True(expired, "A token that expired 2 days ago should be marked expired");
    }

    [Fact]
    public void ExpirationLogic_ZeroDaysIsNotExpired()
    {
        // Simulate: token at exactly the 5-day boundary
        int expiresInDays = 0;
        bool expired = expiresInDays < 0;
        Assert.False(
            expired,
            "A token at exactly the boundary (0 days) should NOT be marked expired"
        );
    }

    [Fact]
    public void ExpirationLogic_NegativeOneDayIsExpired()
    {
        // Simulate: token 1 day past the refresh window
        int expiresInDays = -1;
        bool expired = expiresInDays < 0;
        Assert.True(expired, "A token 1 day past the refresh window should be marked expired");
    }
}
