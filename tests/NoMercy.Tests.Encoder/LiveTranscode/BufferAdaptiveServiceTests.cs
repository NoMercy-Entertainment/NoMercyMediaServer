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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class BufferAdaptiveServiceTests
{
    private static LiveQuality MakeQuality(string id = "1080p", int bitrateKbps = 8000) =>
        new(
            id,
            id,
            1920,
            1080,
            VideoCodecType.H264,
            bitrateKbps,
            "libx264",
            false,
            2.0,
            true
        );

    private static LiveQuality MakeLowQuality() =>
        new(
            "720p",
            "720p",
            1280,
            720,
            VideoCodecType.H264,
            3000,
            "libx264",
            false,
            3.0,
            true
        );

    private static MediaInfo MakeMediaInfo() =>
        new(
            "/media/test.mkv",
            "matroska,webm",
            TimeSpan.FromMinutes(90),
            10000,
            1_000_000_000L,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    8000
                ),
            ],
            [
                new(
                    1,
                    "aac",
                    2,
                    48000,
                    192,
                    "eng",
                    true,
                    false
                ),
            ],
            [],
            []
        );

    private static ClientCapabilities MakeClientCapabilities() =>
        new(
            [VideoCodecType.H264],
            [AudioCodecType.Aac],
            ["mkv", "mp4"],
            1920,
            1080,
            false,
            false,
            0
        );

    private static LiveStreamingService NewStreamingService()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        return new(
            NullLogger<LiveStreamingService>.Instance,
            storage,
            TestStorageFactory.CreateSegmentInventory(storage)
        );
    }

    private static BufferAdaptiveService BuildService(
        ILiveStreamingService streamingService,
        ILiveQualitySelector? qualitySelector = null,
        LiveSessionLimits? limits = null
    )
    {
        LiveSessionLimits sessionLimits = limits ?? new();
        BufferManager bufferManager = new(sessionLimits);
        SpeedIndex speedIndex = new(new());
        Mock<IResourceBudget> budgetMock = new();

        ILiveQualitySelector selector = qualitySelector ?? Mock.Of<ILiveQualitySelector>();

        return new(
            streamingService,
            selector,
            bufferManager,
            speedIndex,
            budgetMock.Object,
            sessionLimits,
            NullLogger<BufferAdaptiveService>.Instance
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend: over-buffered session gets suspended
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_OverBufferedSession_GetsSuspended()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Simulate 40 s transcoded, 0 s reported — buffer = 40 s (above suspend threshold of 30)
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000));

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume: drained-buffer session gets resumed
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_SuspendedSessionWithLowBuffer_GetsResumed()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Buffered);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Simulate 10 s transcoded, 0 s reported — buffer = 10 s (below resume threshold of 15)
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(10), "/tmp/seg0.ts", 1000));

        bool runnerSpawned = false;
        session.AttachRunnerFactory(
            (_, _) =>
            {
                runnerSpawned = true;
                return Task.CompletedTask;
            }
        );

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        // State flips synchronously; the fire-and-forget runner needs a moment.
        await Task.Delay(100);

        session.State.Should().Be(LiveSessionState.Transcoding);
        runnerSpawned.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Quality drop: low buffer triggers ChangeQualityAsync to next-lower tier
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_LowBuffer_DropsToLowerQuality()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality highQuality = MakeQuality("1080p");
        LiveQuality lowQuality = MakeLowQuality();

        LiveSession session = new(Ulid.NewUlid().ToString(), highQuality);
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        streamingService.StampRequestContext(
            session.SessionId,
            MakeMediaInfo(),
            MakeClientCapabilities()
        );

        // Simulate 4 s of buffer — triggers DropQuality (below 5 s threshold)
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(4), "/tmp/seg0.ts", 1000));
        // Report 0 position so BufferAhead = 4 s

        Mock<ILiveQualitySelector> selectorMock = new();
        selectorMock
            .Setup(s =>
                s.GetAvailableQualities(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns([highQuality, lowQuality]);

        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        BufferAdaptiveService service = BuildService(streamingService, selectorMock.Object);

        await service.EvaluateAllAsync(CancellationToken.None);

        session.CurrentQuality.Id.Should().Be("720p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // No action: healthy buffer (20 s, not suspended) → no change
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_HealthyBuffer_NoAction()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // 20 s buffered — within the healthy range
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(20), "/tmp/seg0.ts", 1000));

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Complete sessions are skipped
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_CompleteSession_IsSkipped()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Mark as complete so it drains
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000));
        session.Complete();

        if (streamingService.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime))
            runtime.MarkComplete();

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        // Complete sessions are skipped — state should not have been flipped to Buffered
        // even though buffer is way ahead (the session is done)
        session.State.Should().NotBe(LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static Mock<ILiveQualitySelector> BuildNetworkAwareSelectorMock(
        LiveQuality[] available,
        LiveQuality bandwidthFit
    )
    {
        Mock<ILiveQualitySelector> selectorMock = new();
        selectorMock
            .Setup(s =>
                s.GetAvailableQualities(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(available);
        selectorMock
            .Setup(s =>
                s.SelectForBandwidth(
                    It.IsAny<LiveQuality[]>(),
                    It.IsAny<int>(),
                    It.IsAny<double>(),
                    It.IsAny<LiveQuality>()
                )
            )
            .Returns(bandwidthFit);
        return selectorMock;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: a network-limited client (fresh health, low downlink) drops
    // to the bandwidth-fitting tier, NOT Suspend — even though the encoder-lead
    // buffer is large enough to trigger Suspend on its own.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_NetworkLimitedClient_DropsToBandwidthFittingTier_NotSuspend()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality quality1080 = MakeQuality("1080p", 8000);
        LiveQuality quality720 = MakeQuality("720p", 4000);
        LiveQuality quality480 = MakeQuality("480p", 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(Ulid.NewUlid().ToString(), quality1080);
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));
        streamingService.StampRequestContext(
            session.SessionId,
            MakeMediaInfo(),
            MakeClientCapabilities()
        );

        // Encoder-lead buffer is 40 s — above SuspendAboveSeconds (30) — so the
        // encoder-capacity axis alone would suspend this session.
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000));

        // Fresh client health: healthy download buffer (not near-stall) but a
        // downlink that only fits the lowest tier.
        session.ReportClientBufferHealth(TimeSpan.FromSeconds(20), 3000);

        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available,
            quality480
        );

        BufferAdaptiveService service = BuildService(streamingService, selectorMock.Object);

        await service.EvaluateAllAsync(CancellationToken.None);
        await Task.Delay(50);

        session.CurrentQuality.Id.Should().Be("480p");
        session.State.Should().NotBe(LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: client download-buffer near-stall triggers an emergency
    // drop straight to the lowest tier, regardless of downlink.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_ClientBufferNearStall_EmergencyDropsToLowestTier()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality quality1080 = MakeQuality("1080p", 8000);
        LiveQuality quality720 = MakeQuality("720p", 4000);
        LiveQuality quality480 = MakeQuality("480p", 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(Ulid.NewUlid().ToString(), quality1080);
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));
        streamingService.StampRequestContext(
            session.SessionId,
            MakeMediaInfo(),
            MakeClientCapabilities()
        );

        // Encoder-lead buffer is healthy (15 s) — the encoder-capacity axis
        // would not act on its own.
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(15), "/tmp/seg0.ts", 1000));

        // Client's download buffer is draining toward a stall (below the 2 s
        // default emergency threshold) — bandwidth itself is irrelevant here.
        session.ReportClientBufferHealth(TimeSpan.FromSeconds(1), 20000);

        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available,
            quality1080
        );

        BufferAdaptiveService service = BuildService(streamingService, selectorMock.Object);

        await service.EvaluateAllAsync(CancellationToken.None);
        await Task.Delay(50);

        session.CurrentQuality.Id.Should().Be("480p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: a sustained recovered network raises quality one tier once
    // the hysteresis count is met — not before (no flap).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_RecoveredNetworkSustainedNSweeps_RaisesOneTier_NotBeforeHysteresis()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality quality1080 = MakeQuality("1080p", 8000);
        LiveQuality quality720 = MakeQuality("720p", 4000);
        LiveQuality quality480 = MakeQuality("480p", 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(Ulid.NewUlid().ToString(), quality480);
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));
        streamingService.StampRequestContext(
            session.SessionId,
            MakeMediaInfo(),
            MakeClientCapabilities()
        );

        // Encoder-lead buffer stays in the healthy "no action" band across sweeps.
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(15), "/tmp/seg0.ts", 1000));

        // Downlink comfortably fits the top tier and the client buffer is
        // healthy — sustained across every sweep below.
        session.ReportClientBufferHealth(TimeSpan.FromSeconds(20), 15000);

        session.AttachRunnerFactory((_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available,
            quality1080
        );

        LiveSessionLimits limits = new();
        BufferAdaptiveService service = BuildService(streamingService, selectorMock.Object, limits);

        // RaiseSustainSweeps defaults to 3 — the first two sweeps must not raise.
        await service.EvaluateAllAsync(CancellationToken.None);
        session.CurrentQuality.Id.Should().Be("480p");

        await service.EvaluateAllAsync(CancellationToken.None);
        session.CurrentQuality.Id.Should().Be("480p");

        // Third consecutive eligible sweep meets the hysteresis count — raises
        // exactly ONE tier up (720p), not straight to the bandwidth-fitting 1080p.
        await service.EvaluateAllAsync(CancellationToken.None);
        await Task.Delay(50);
        session.CurrentQuality.Id.Should().Be("720p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Backward compatibility: a session with no fresh client-health report
    // (an old client that never calls ReportClientBufferHealth) falls back to
    // today's encoder-lead-only Suspend behavior, unchanged.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_NoFreshClientHealth_FallsBackToEncoderLeadSuspend()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Old client never calls ReportClientBufferHealth — ClientBufferedAhead
        // and ObservedBandwidthKbps stay at their defaults, and
        // HasFreshClientHealth stays false, exactly as for a pre-upgrade client.
        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000));

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Buffered);
    }

    [Fact]
    public async Task EvaluateAll_StaleClientHealthReport_FallsBackToEncoderLeadSuspend()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // A report landed once, but it is older than the staleness window by
        // the time the sweep runs — treated exactly like no report at all.
        session.ReportClientBufferHealth(TimeSpan.FromSeconds(1), 1000);
        await Task.Delay(50);

        session.PushSegment(new(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000));

        LiveSessionLimits limits = new();
        limits.Buffer.ClientHealthStalenessSeconds = 0;
        BufferAdaptiveService service = BuildService(streamingService, limits: limits);

        await service.EvaluateAllAsync(CancellationToken.None);

        session.State.Should().Be(LiveSessionState.Buffered);
    }
}
