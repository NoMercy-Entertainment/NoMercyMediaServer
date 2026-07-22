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
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(minutes: 5);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ── Method normalization ────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["POST", "post"])]
    [InlineData(data: ["POST", "Post"])]
    [InlineData(data: ["GET", "get"])]
    [InlineData(data: ["DELETE", "delete"])]
    [InlineData(data: ["PUT", "Put"])]
    public void Sign_method_is_normalized_to_uppercase(string upper, string mixed)
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string upperSig = signer.Sign(method: upper, path: "/x", timestamp: ts, body: body);
        string mixedSig = signer.Sign(method: mixed, path: "/x", timestamp: ts, body: body);

        upperSig.Should().Be(expected: mixedSig);
    }

    [Fact]
    public void Verify_method_is_normalized_to_uppercase()
    {
        // Sign with "post" but verify with "POST" — must succeed because the
        // method is normalized on both sides.
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string signature = signer.Sign(method: "post", path: "/x", timestamp: ts, body: body);
        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: signature, replayWindow: Window);

        ok.Should().BeTrue();
    }

    // ── Path is case-sensitive ──────────────────────────────────────────────

    [Fact]
    public void Path_match_is_case_sensitive()
    {
        // Path is NOT normalized — /api and /API produce different signatures.
        // Pin so a future "normalize" change is intentional.
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string lowerSig = signer.Sign(method: "GET", path: "/api", timestamp: ts, body: body);
        string upperSig = signer.Sign(method: "GET", path: "/API", timestamp: ts, body: body);

        lowerSig.Should().NotBe(unexpected: upperSig);
    }

    // ── Length-mismatch signature ───────────────────────────────────────────

    [Fact]
    public void Verify_signature_with_wrong_length_returns_false()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string realSig = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: body);
        // Truncate one byte off the base64 string.
        string truncated = realSig[..^1];

        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: truncated, replayWindow: Window);

        ok.Should().BeFalse();
    }

    [Fact]
    public void Verify_signature_with_extra_chars_returns_false()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string realSig = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: body);
        string padded = realSig + "X";

        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: padded, replayWindow: Window);

        ok.Should().BeFalse();
    }

    // ── Body differentiation ────────────────────────────────────────────────

    [Fact]
    public void Different_bodies_yield_different_signatures()
    {
        // Body is part of the string-to-sign via SHA256(body) — pin it.
        HmacSigner signer = new(secret: Secret);
        long ts = Now();

        string sigA = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: Encoding.UTF8.GetBytes(s: "{\"a\":1}"));
        string sigB = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: Encoding.UTF8.GetBytes(s: "{\"a\":2}"));

        sigA.Should().NotBe(unexpected: sigB);
    }

    [Fact]
    public void Empty_body_and_whitespace_body_yield_different_signatures()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now();

        string sigEmpty = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: []);
        string sigSpace = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: Encoding.UTF8.GetBytes(s: " "));

        sigEmpty.Should().NotBe(unexpected: sigSpace);
    }

    // ── Replay window boundaries ────────────────────────────────────────────

    [Fact]
    public void Verify_age_zero_passes()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now();
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string sig = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: body);

        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: sig, replayWindow: Window);

        ok.Should().BeTrue();
    }

    [Fact]
    public void Verify_age_at_exactly_window_size_passes()
    {
        // ageSeconds == window → still inside (the predicate is `> window`).
        HmacSigner signer = new(secret: Secret);
        long ts = Now() - (long)Window.TotalSeconds;
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string sig = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: body);

        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: sig, replayWindow: Window);

        ok.Should().BeTrue();
    }

    [Fact]
    public void Verify_age_one_second_past_window_fails()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = Now() - (long)Window.TotalSeconds - 1;
        byte[] body = Encoding.UTF8.GetBytes(s: "{}");

        string sig = signer.Sign(method: "POST", path: "/x", timestamp: ts, body: body);

        bool ok = signer.Verify(method: "POST", path: "/x", timestamp: ts, body: body, signature: sig, replayWindow: Window);

        ok.Should().BeFalse();
    }
}
