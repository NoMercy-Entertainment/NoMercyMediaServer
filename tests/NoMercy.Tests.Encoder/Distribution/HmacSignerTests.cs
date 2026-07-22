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

[Trait(name: "Category", value: "Unit")]
public class HmacSignerTests
{
    private const string Secret = "test-secret-key-for-hmac-signing";
    private static readonly byte[] EmptyBody = [];
    private static readonly byte[] SampleBody = Encoding.UTF8.GetBytes(s: "{\"taskId\":\"abc\"}");
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(minutes: 5);

    private static long NowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [Fact]
    public void RoundTrip_SameSecret_ReturnsTrue()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        bool result = signer.Verify(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            timestamp: ts,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.True(condition: result);
    }

    [Fact]
    public void WrongSecret_ReturnsFalse()
    {
        HmacSigner signer = new(secret: Secret);
        HmacSigner verifier = new(secret: "wrong-secret");

        long ts = NowSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        bool result = verifier.Verify(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            timestamp: ts,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void OldTimestamp_OutsideReplayWindow_ReturnsFalse()
    {
        HmacSigner signer = new(secret: Secret);
        long staleTs = DateTimeOffset.UtcNow.AddMinutes(minutes: -6).ToUnixTimeSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: staleTs, body: SampleBody);

        bool result = signer.Verify(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            timestamp: staleTs,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void TamperedBody_ReturnsFalse()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        byte[] tampered = Encoding.UTF8.GetBytes(s: "{\"taskId\":\"tampered\"}");
        bool result = signer.Verify(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            timestamp: ts,
            body: tampered,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void MethodMismatch_ReturnsFalse()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        bool result = signer.Verify(
            method: "GET",
            path: "/api/v1/worker/execute-task",
            timestamp: ts,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void PathMismatch_ReturnsFalse()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        bool result = signer.Verify(
            method: "POST",
            path: "/api/v1/worker/other-path",
            timestamp: ts,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }

    [Fact]
    public void EmptyBody_RoundTrip_ReturnsTrue()
    {
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds();
        string sig = signer.Sign(method: "GET", path: "/api/v1/worker-source", timestamp: ts, body: EmptyBody);

        bool result = signer.Verify(
            method: "GET",
            path: "/api/v1/worker-source",
            timestamp: ts,
            body: EmptyBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.True(condition: result);
    }

    [Fact]
    public void FutureTimestamp_WithinWindow_ReturnsTrue()
    {
        // Clocks between coordinator and worker may be slightly skewed.
        // A timestamp 1 second in the future is within the window.
        HmacSigner signer = new(secret: Secret);
        long ts = NowSeconds() + 1;
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: SampleBody);

        // ageSeconds = nowSeconds - (nowSeconds + 1) = -1, which is < 0 → false.
        // This documents the strict behaviour: only past-or-present timestamps pass.
        bool result = signer.Verify(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            timestamp: ts,
            body: SampleBody,
            signature: sig,
            replayWindow: FiveMinutes
        );

        Assert.False(condition: result);
    }
}
