using System.Text;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// Branch-coverage gaps for <see cref="HmacSigner"/>:
///
/// • HTTP method case normalization — Sign uppercases the method via
///   ToUpperInvariant, so post + POST produce the same signature.
/// • Path is NOT normalized — case-sensitive comparison documented.
/// • Length-mismatch fast-fail — a corrupted signature of the wrong length
///   short-circuits before FixedTimeEquals.
/// • Body-hash differentiation — same method/path/timestamp/key, different
///   body → different signature (proves body is in the string-to-sign).
/// • Replay-window boundary — exactly at window expiry passes, one second
///   past expires.
/// </summary>
public class HmacSignerBranchTests
{
    private const string Secret = "branch-test-signing-key-32-bytes!";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ── Method normalization ────────────────────────────────────────────────

    [Theory]
    [InlineData("POST", "post")]
    [InlineData("POST", "Post")]
    [InlineData("GET", "get")]
    [InlineData("DELETE", "delete")]
    [InlineData("PUT", "Put")]
    public void Sign_method_is_normalized_to_uppercase(string upper, string mixed)
    {
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string upperSig = signer.Sign(upper, "/x", ts, body);
        string mixedSig = signer.Sign(mixed, "/x", ts, body);

        upperSig.Should().Be(mixedSig);
    }

    [Fact]
    public void Verify_method_is_normalized_to_uppercase()
    {
        // Sign with "post" but verify with "POST" — must succeed because the
        // method is normalized on both sides.
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string signature = signer.Sign("post", "/x", ts, body);
        bool ok = signer.Verify("POST", "/x", ts, body, signature, Window);

        ok.Should().BeTrue();
    }

    // ── Path is case-sensitive ──────────────────────────────────────────────

    [Fact]
    public void Path_match_is_case_sensitive()
    {
        // Path is NOT normalized — /api and /API produce different signatures.
        // Pin so a future "normalize" change is intentional.
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string lowerSig = signer.Sign("GET", "/api", ts, body);
        string upperSig = signer.Sign("GET", "/API", ts, body);

        lowerSig.Should().NotBe(upperSig);
    }

    // ── Length-mismatch signature ───────────────────────────────────────────

    [Fact]
    public void Verify_signature_with_wrong_length_returns_false()
    {
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string realSig = signer.Sign("POST", "/x", ts, body);
        // Truncate one byte off the base64 string.
        string truncated = realSig[..^1];

        bool ok = signer.Verify("POST", "/x", ts, body, truncated, Window);

        ok.Should().BeFalse();
    }

    [Fact]
    public void Verify_signature_with_extra_chars_returns_false()
    {
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string realSig = signer.Sign("POST", "/x", ts, body);
        string padded = realSig + "X";

        bool ok = signer.Verify("POST", "/x", ts, body, padded, Window);

        ok.Should().BeFalse();
    }

    // ── Body differentiation ────────────────────────────────────────────────

    [Fact]
    public void Different_bodies_yield_different_signatures()
    {
        // Body is part of the string-to-sign via SHA256(body) — pin it.
        HmacSigner signer = new(Secret);
        long ts = Now();

        string sigA = signer.Sign("POST", "/x", ts, Encoding.UTF8.GetBytes("{\"a\":1}"));
        string sigB = signer.Sign("POST", "/x", ts, Encoding.UTF8.GetBytes("{\"a\":2}"));

        sigA.Should().NotBe(sigB);
    }

    [Fact]
    public void Empty_body_and_whitespace_body_yield_different_signatures()
    {
        HmacSigner signer = new(Secret);
        long ts = Now();

        string sigEmpty = signer.Sign("POST", "/x", ts, []);
        string sigSpace = signer.Sign("POST", "/x", ts, Encoding.UTF8.GetBytes(" "));

        sigEmpty.Should().NotBe(sigSpace);
    }

    // ── Replay window boundaries ────────────────────────────────────────────

    [Fact]
    public void Verify_age_zero_passes()
    {
        HmacSigner signer = new(Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string sig = signer.Sign("POST", "/x", ts, body);

        bool ok = signer.Verify("POST", "/x", ts, body, sig, Window);

        ok.Should().BeTrue();
    }

    [Fact]
    public void Verify_age_at_exactly_window_size_passes()
    {
        // ageSeconds == window → still inside (the predicate is `> window`).
        HmacSigner signer = new(Secret);
        long ts = Now() - (long)Window.TotalSeconds;
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string sig = signer.Sign("POST", "/x", ts, body);

        bool ok = signer.Verify("POST", "/x", ts, body, sig, Window);

        ok.Should().BeTrue();
    }

    [Fact]
    public void Verify_age_one_second_past_window_fails()
    {
        HmacSigner signer = new(Secret);
        long ts = Now() - (long)Window.TotalSeconds - 1;
        byte[] body = Encoding.UTF8.GetBytes("{}");

        string sig = signer.Sign("POST", "/x", ts, body);

        bool ok = signer.Verify("POST", "/x", ts, body, sig, Window);

        ok.Should().BeFalse();
    }
}
