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

using NoMercy.Encoder.Composition;

namespace NoMercy.Tests.Encoder.Composition;

/// <summary>
/// EncoderOptions is the DI-registered config object every encoder
/// component reaches through. Two resolver properties deserve tests:
///
///   - FfmpegPath / FfprobePath throw a clear InvalidOperationException
///     when the overrides aren't set, instead of propagating a
///     cryptic "process not found" from FfmpegExecutor later.
///   - ResolvedLiveTranscodeCachePath defaults to a sane temp location
///     so live sessions never write to the repo directory by accident.
/// </summary>
public class EncoderOptionsTests
{
    [Fact]
    public void FfmpegPath_WithoutOverride_ThrowsWithActionableMessage()
    {
        EncoderOptions options = new();

        Action act = () => _ = options.FfmpegPath;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*AddNoMercyEncoder*", "message must point the caller at the DI helper");
    }

    [Fact]
    public void FfprobePath_WithoutOverride_Throws()
    {
        EncoderOptions options = new();

        Action act = () => _ = options.FfprobePath;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FfmpegPath_WithOverride_ReturnsOverride()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "/opt/ffmpeg/bin/ffmpeg" };

        options.FfmpegPath.Should().Be("/opt/ffmpeg/bin/ffmpeg");
    }

    [Fact]
    public void FfprobePath_WithOverride_ReturnsOverride()
    {
        EncoderOptions options = new() { FfprobePathOverride = "/opt/ffmpeg/bin/ffprobe" };

        options.FfprobePath.Should().Be("/opt/ffmpeg/bin/ffprobe");
    }

    [Fact]
    public void ResolvedLiveTranscodeCachePath_UnsetDefault_UsesTempDir()
    {
        EncoderOptions options = new();

        string resolved = options.ResolvedLiveTranscodeCachePath;

        resolved.Should().EndWith("nomercy-live");
        resolved.Should().StartWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ResolvedLiveTranscodeCachePath_WithOverride_UsesOverride()
    {
        EncoderOptions options = new() { LiveTranscodeCachePath = "/custom/live-cache" };

        options.ResolvedLiveTranscodeCachePath.Should().Be("/custom/live-cache");
    }

    [Fact]
    public void NotificationWebhookUrls_DefaultsToEmpty()
    {
        // Must be a mutable, initialized list — callers append URLs.
        // Null-default would NRE the first Add().
        EncoderOptions options = new();

        options.NotificationWebhookUrls.Should().NotBeNull();
        options.NotificationWebhookUrls.Should().BeEmpty();

        options.NotificationWebhookUrls.Add("https://example.com/hook");
        options.NotificationWebhookUrls.Should().ContainSingle();
    }

    [Fact]
    public void DefaultSegmentDuration_IsFourSeconds()
    {
        // 4s is the Apple HLS authoring spec default. EncoderOptions's
        // value drives Live sessions (OutputPlan has its own 6s default
        // for file encodes — different use case).
        EncoderOptions options = new();
        options.DefaultSegmentDurationSeconds.Should().Be(4);
    }

    [Fact]
    public void BufferAheadWindow_MinBelowMax()
    {
        // BufferManager relies on the invariant Min < Max — violating it
        // means the "suspend" and "resume" states overlap and the live
        // session thrashes between them.
        EncoderOptions options = new();
        options.MinBufferAheadSeconds.Should().BeLessThan(options.MaxBufferAheadSeconds);
    }

    [Fact]
    public void AutoCalibrate_DefaultsTrue()
    {
        // Fresh install should benchmark hardware on first start. Opt-out
        // for test containers / CI by setting this to false.
        EncoderOptions options = new();
        options.AutoCalibrate.Should().BeTrue();
    }

    // ── Distributed encoding ────────────────────────────────────────────────

    [Fact]
    public void IsDistributedEncodingEnabled_NoSigningKey_False()
    {
        // No signing key configured → distribution stays OFF. The API layer
        // uses this flag to gate worker-registration endpoints.
        EncoderOptions options = new();

        options.IsDistributedEncodingEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDistributedEncodingEnabled_WhitespaceSigningKey_False()
    {
        // Empty / whitespace key is NOT a valid HMAC key — treat as unset.
        EncoderOptions options = new() { DistributedEncodingSigningKey = "   " };

        options.IsDistributedEncodingEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsDistributedEncodingEnabled_WithSigningKey_True()
    {
        EncoderOptions options = new() { DistributedEncodingSigningKey = "shared-hmac-secret" };

        options.IsDistributedEncodingEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsCoordinatorMode_NoKey_False()
    {
        EncoderOptions options = new();

        options.IsCoordinatorMode.Should().BeFalse();
    }

    [Fact]
    public void IsCoordinatorMode_KeyWithoutCoordinatorUrl_True()
    {
        // Signing key set + no upstream coordinator URL ⇒ this server IS the
        // coordinator. The JsonRemoteWorkerRegistry only loads in this mode.
        EncoderOptions options = new() { DistributedEncodingSigningKey = "k" };

        options.IsCoordinatorMode.Should().BeTrue();
    }

    [Fact]
    public void IsCoordinatorMode_KeyAndCoordinatorUrlBothSet_False()
    {
        // Worker mode: this server points at an upstream coordinator. It is
        // NOT itself a coordinator, even though it has the signing key.
        EncoderOptions options = new()
        {
            DistributedEncodingSigningKey = "k",
            CoordinatorUrl = "https://upstream.example.com",
        };

        options.IsCoordinatorMode.Should().BeFalse();
    }

    // ── Subscriber toggles default ON ───────────────────────────────────────

    [Fact]
    public void IntroDetectSubscriber_DefaultsOn()
    {
        new EncoderOptions().EnableIntroDetectSubscriber.Should().BeTrue();
    }

    [Fact]
    public void OcrPostEncodeSubscriber_DefaultsOn()
    {
        new EncoderOptions().EnableOcrPostEncodeSubscriber.Should().BeTrue();
    }

    [Fact]
    public void WorkerRegistryPath_DefaultsUnderLocalAppData()
    {
        // Fresh install → workers.json lands in LocalApplicationData under
        // NoMercy/distribution. Anything outside the user's profile would
        // leak persistence across machine reinstalls.
        EncoderOptions options = new();

        options.WorkerRegistryPath.Should().Contain("NoMercy");
        options.WorkerRegistryPath.Should().Contain("distribution");
        options.WorkerRegistryPath.Should().EndWith("workers.json");
    }
}
