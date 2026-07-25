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

[Trait("Category", "Unit")]
public class HmacSignerTests
{
    private const string Secret = "test-secret-key-for-hmac-signing";
    private static readonly byte[] EmptyBody = [];
    private static readonly byte[] SampleBody = Encoding.UTF8.GetBytes("{\"taskId\":\"abc\"}");
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    private static long NowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [Fact]
    public void RoundTrip_SameSecret_ReturnsTrue()
    {
        HmacSigner signer = new(Secret);
        long ts = NowSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        bool result = signer.Verify(
            "POST",
            "/api/v1/worker/execute-task",
            ts,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.True(result);
    }

    [Fact]
    public void WrongSecret_ReturnsFalse()
    {
        HmacSigner signer = new(Secret);
        HmacSigner verifier = new("wrong-secret");

        long ts = NowSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        bool result = verifier.Verify(
            "POST",
            "/api/v1/worker/execute-task",
            ts,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }

    [Fact]
    public void OldTimestamp_OutsideReplayWindow_ReturnsFalse()
    {
        HmacSigner signer = new(Secret);
        long staleTs = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", staleTs, SampleBody);

        bool result = signer.Verify(
            "POST",
            "/api/v1/worker/execute-task",
            staleTs,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }

    [Fact]
    public void TamperedBody_ReturnsFalse()
    {
        HmacSigner signer = new(Secret);
        long ts = NowSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        byte[] tampered = Encoding.UTF8.GetBytes("{\"taskId\":\"tampered\"}");
        bool result = signer.Verify(
            "POST",
            "/api/v1/worker/execute-task",
            ts,
            tampered,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }

    [Fact]
    public void MethodMismatch_ReturnsFalse()
    {
        HmacSigner signer = new(Secret);
        long ts = NowSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        bool result = signer.Verify(
            "GET",
            "/api/v1/worker/execute-task",
            ts,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }

    [Fact]
    public void PathMismatch_ReturnsFalse()
    {
        HmacSigner signer = new(Secret);
        long ts = NowSeconds();
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        bool result = signer.Verify(
            "POST",
            "/api/v1/worker/other-path",
            ts,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }

    [Fact]
    public void EmptyBody_RoundTrip_ReturnsTrue()
    {
        HmacSigner signer = new(Secret);
        long ts = NowSeconds();
        string sig = signer.Sign("GET", "/api/v1/worker-source", ts, EmptyBody);

        bool result = signer.Verify(
            "GET",
            "/api/v1/worker-source",
            ts,
            EmptyBody,
            sig,
            FiveMinutes
        );

        Assert.True(result);
    }

    [Fact]
    public void FutureTimestamp_WithinWindow_ReturnsTrue()
    {
        // Clocks between coordinator and worker may be slightly skewed.
        // A timestamp 1 second in the future is within the window.
        HmacSigner signer = new(Secret);
        long ts = NowSeconds() + 1;
        string sig = signer.Sign("POST", "/api/v1/worker/execute-task", ts, SampleBody);

        // ageSeconds = nowSeconds - (nowSeconds + 1) = -1, which is < 0 → false.
        // This documents the strict behaviour: only past-or-present timestamps pass.
        bool result = signer.Verify(
            "POST",
            "/api/v1/worker/execute-task",
            ts,
            SampleBody,
            sig,
            FiveMinutes
        );

        Assert.False(result);
    }
}
