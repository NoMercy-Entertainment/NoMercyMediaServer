using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// ClusterTokenHolder carries the most recently issued cluster token and the
/// derived HmacSigner. Used by the HMAC middleware to validate inbound
/// requests with the live key; wrong behaviour here surfaces as silently
/// rejected (or accepted) signed requests.
/// </summary>
public class ClusterTokenHolderTests
{
    [Fact]
    public void Initial_NoToken_HasValidTokenFalse()
    {
        ClusterTokenHolder holder = new();

        holder.Current.Should().BeNull();
        holder.HasValidToken.Should().BeFalse();
        holder.GetSigner().Should().BeNull();
        holder.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Update_ValidFutureToken_HasValidTokenTrue()
    {
        ClusterTokenHolder holder = new();
        ClusterToken token = new(
            Secret: "future-secret",
            ExpiresAt: DateTime.UtcNow.AddHours(1),
            Scopes: ["encode"]
        );

        holder.Update(token);

        holder.Current.Should().Be(token);
        holder.HasValidToken.Should().BeTrue();
        holder.GetSigner().Should().NotBeNull();
    }

    [Fact]
    public void Update_AlreadyExpiredToken_HasValidTokenFalse()
    {
        ClusterTokenHolder holder = new();
        ClusterToken expired = new(
            Secret: "stale-secret",
            ExpiresAt: DateTime.UtcNow.AddHours(-1),
            Scopes: []
        );

        holder.Update(expired);

        holder.Current.Should().Be(expired);
        holder.HasValidToken.Should().BeFalse();
        // Signer is gated by HasValidToken — even though Update built one,
        // GetSigner returns null while the token is expired.
        holder.GetSigner().Should().BeNull();
    }

    [Fact]
    public void Update_NewToken_BuildsFreshSigner()
    {
        ClusterTokenHolder holder = new();
        ClusterToken first = new("first-secret", DateTime.UtcNow.AddHours(1), []);
        ClusterToken second = new("second-secret", DateTime.UtcNow.AddHours(1), []);

        holder.Update(first);
        HmacSigner? firstSigner = holder.GetSigner();
        holder.Update(second);
        HmacSigner? secondSigner = holder.GetSigner();

        firstSigner.Should().NotBeNull();
        secondSigner.Should().NotBeNull();
        // Different signers for different secrets.
        secondSigner.Should().NotBeSameAs(firstSigner);
    }

    [Fact]
    public void IsRevoked_SettablePropertyDefaultsFalse()
    {
        ClusterTokenHolder holder = new();
        holder.IsRevoked.Should().BeFalse();

        holder.IsRevoked = true;
        holder.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void HasValidToken_ExactExpiryBoundary_False()
    {
        // ExpiresAt > DateTime.UtcNow — strictly greater than. A token whose
        // ExpiresAt has already passed must be invalid.
        ClusterTokenHolder holder = new();
        ClusterToken expired = new("s", DateTime.UtcNow.AddMilliseconds(-1), []);

        holder.Update(expired);

        holder.HasValidToken.Should().BeFalse();
    }
}
