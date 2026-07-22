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
            Id: id,
            Label: id,
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: bitrateKbps,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static LiveQuality MakeLowQuality() =>
        new(
            Id: "720p",
            Label: "720p",
            Width: 1280,
            Height: 720,
            Codec: VideoCodecType.H264,
            BitrateKbps: 3000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 3.0,
            CanRealtime: true
        );

    private static MediaInfo MakeMediaInfo() =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska,webm",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 10000,
            FileSizeBytes: 1_000_000_000L,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 8000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "aac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 192,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    private static ClientCapabilities MakeClientCapabilities() =>
        new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mkv", "mp4"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: false,
            Supports10Bit: false,
            MaxBitrateKbps: 0
        );

    private static LiveStreamingService NewStreamingService()
    {
        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        return new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: TestStorageFactory.CreateSegmentInventory(storage: storage)
        );
    }

    private static BufferAdaptiveService BuildService(
        ILiveStreamingService streamingService,
        ILiveQualitySelector? qualitySelector = null,
        LiveSessionLimits? limits = null
    )
    {
        LiveSessionLimits sessionLimits = limits ?? new();
        BufferManager bufferManager = new(limits: sessionLimits);
        SpeedIndex speedIndex = new(Measurements: new());
        Mock<IResourceBudget> budgetMock = new();

        ILiveQualitySelector selector = qualitySelector ?? Mock.Of<ILiveQualitySelector>();

        return new(
            streamingService: streamingService,
            qualitySelector: selector,
            bufferManager: bufferManager,
            speedIndex: speedIndex,
            resourceBudget: budgetMock.Object,
            limits: sessionLimits,
            logger: NullLogger<BufferAdaptiveService>.Instance
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend: over-buffered session gets suspended
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_OverBufferedSession_GetsSuspended()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Simulate 40 s transcoded, 0 s reported — buffer = 40 s (above suspend threshold of 30)
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 40), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        BufferAdaptiveService service = BuildService(streamingService: streamingService);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resume: drained-buffer session gets resumed
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_SuspendedSessionWithLowBuffer_GetsResumed()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Buffered);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Simulate 10 s transcoded, 0 s reported — buffer = 10 s (below resume threshold of 15)
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 10), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        bool runnerSpawned = false;
        session.AttachRunnerFactory(
            factory: (_, _) =>
            {
                runnerSpawned = true;
                return Task.CompletedTask;
            }
        );

        BufferAdaptiveService service = BuildService(streamingService: streamingService);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        // State flips synchronously; the fire-and-forget runner needs a moment.
        await Task.Delay(millisecondsDelay: 100);

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
        runnerSpawned.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Quality drop: low buffer triggers ChangeQualityAsync to next-lower tier
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_LowBuffer_DropsToLowerQuality()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality highQuality = MakeQuality(id: "1080p");
        LiveQuality lowQuality = MakeLowQuality();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: highQuality);
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        streamingService.StampRequestContext(
            sessionId: session.SessionId,
            mediaInfo: MakeMediaInfo(),
            client: MakeClientCapabilities()
        );

        // Simulate 4 s of buffer — triggers DropQuality (below 5 s threshold)
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 4), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));
        // Report 0 position so BufferAhead = 4 s

        Mock<ILiveQualitySelector> selectorMock = new();
        selectorMock
            .Setup(expression: s =>
                s.GetAvailableQualities(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: [highQuality, lowQuality]);

        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        BufferAdaptiveService service = BuildService(streamingService: streamingService, qualitySelector: selectorMock.Object);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        session.CurrentQuality.Id.Should().Be(expected: "720p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // No action: healthy buffer (20 s, not suspended) → no change
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_HealthyBuffer_NoAction()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // 20 s buffered — within the healthy range
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 20), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        BufferAdaptiveService service = BuildService(streamingService: streamingService);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Complete sessions are skipped
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_CompleteSession_IsSkipped()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Mark as complete so it drains
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 40), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));
        session.Complete();

        if (streamingService.TryGetRuntime(sessionId: session.SessionId, runtime: out LiveRuntimeSession runtime))
            runtime.MarkComplete();

        BufferAdaptiveService service = BuildService(streamingService: streamingService);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        // Complete sessions are skipped — state should not have been flipped to Buffered
        // even though buffer is way ahead (the session is done)
        session.State.Should().NotBe(unexpected: LiveSessionState.Buffered);
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
            .Setup(expression: s =>
                s.GetAvailableQualities(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: available);
        selectorMock
            .Setup(expression: s =>
                s.SelectForBandwidth(
                    It.IsAny<LiveQuality[]>(),
                    It.IsAny<int>(),
                    It.IsAny<double>(),
                    It.IsAny<LiveQuality>()
                )
            )
            .Returns(value: bandwidthFit);
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

        LiveQuality quality1080 = MakeQuality(id: "1080p", bitrateKbps: 8000);
        LiveQuality quality720 = MakeQuality(id: "720p", bitrateKbps: 4000);
        LiveQuality quality480 = MakeQuality(id: "480p", bitrateKbps: 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: quality1080);
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));
        streamingService.StampRequestContext(
            sessionId: session.SessionId,
            mediaInfo: MakeMediaInfo(),
            client: MakeClientCapabilities()
        );

        // Encoder-lead buffer is 40 s — above SuspendAboveSeconds (30) — so the
        // encoder-capacity axis alone would suspend this session.
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 40), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        // Fresh client health: healthy download buffer (not near-stall) but a
        // downlink that only fits the lowest tier.
        session.ReportClientBufferHealth(bufferedAhead: TimeSpan.FromSeconds(seconds: 20), observedBandwidthKbps: 3000);

        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available: available,
            bandwidthFit: quality480
        );

        BufferAdaptiveService service = BuildService(streamingService: streamingService, qualitySelector: selectorMock.Object);

        await service.EvaluateAllAsync(ct: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);

        session.CurrentQuality.Id.Should().Be(expected: "480p");
        session.State.Should().NotBe(unexpected: LiveSessionState.Buffered);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: client download-buffer near-stall triggers an emergency
    // drop straight to the lowest tier, regardless of downlink.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_ClientBufferNearStall_EmergencyDropsToLowestTier()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality quality1080 = MakeQuality(id: "1080p", bitrateKbps: 8000);
        LiveQuality quality720 = MakeQuality(id: "720p", bitrateKbps: 4000);
        LiveQuality quality480 = MakeQuality(id: "480p", bitrateKbps: 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: quality1080);
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));
        streamingService.StampRequestContext(
            sessionId: session.SessionId,
            mediaInfo: MakeMediaInfo(),
            client: MakeClientCapabilities()
        );

        // Encoder-lead buffer is healthy (15 s) — the encoder-capacity axis
        // would not act on its own.
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 15), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        // Client's download buffer is draining toward a stall (below the 2 s
        // default emergency threshold) — bandwidth itself is irrelevant here.
        session.ReportClientBufferHealth(bufferedAhead: TimeSpan.FromSeconds(seconds: 1), observedBandwidthKbps: 20000);

        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available: available,
            bandwidthFit: quality1080
        );

        BufferAdaptiveService service = BuildService(streamingService: streamingService, qualitySelector: selectorMock.Object);

        await service.EvaluateAllAsync(ct: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);

        session.CurrentQuality.Id.Should().Be(expected: "480p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Network axis: a sustained recovered network raises quality one tier once
    // the hysteresis count is met — not before (no flap).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_RecoveredNetworkSustainedNSweeps_RaisesOneTier_NotBeforeHysteresis()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveQuality quality1080 = MakeQuality(id: "1080p", bitrateKbps: 8000);
        LiveQuality quality720 = MakeQuality(id: "720p", bitrateKbps: 4000);
        LiveQuality quality480 = MakeQuality(id: "480p", bitrateKbps: 2000);
        LiveQuality[] available = [quality1080, quality720, quality480];

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: quality480);
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));
        streamingService.StampRequestContext(
            sessionId: session.SessionId,
            mediaInfo: MakeMediaInfo(),
            client: MakeClientCapabilities()
        );

        // Encoder-lead buffer stays in the healthy "no action" band across sweeps.
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 15), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        // Downlink comfortably fits the top tier and the client buffer is
        // healthy — sustained across every sweep below.
        session.ReportClientBufferHealth(bufferedAhead: TimeSpan.FromSeconds(seconds: 20), observedBandwidthKbps: 15000);

        session.AttachRunnerFactory(factory: (_, _) => Task.CompletedTask);

        Mock<ILiveQualitySelector> selectorMock = BuildNetworkAwareSelectorMock(
            available: available,
            bandwidthFit: quality1080
        );

        LiveSessionLimits limits = new();
        BufferAdaptiveService service = BuildService(streamingService: streamingService, qualitySelector: selectorMock.Object, limits: limits);

        // RaiseSustainSweeps defaults to 3 — the first two sweeps must not raise.
        await service.EvaluateAllAsync(ct: CancellationToken.None);
        session.CurrentQuality.Id.Should().Be(expected: "480p");

        await service.EvaluateAllAsync(ct: CancellationToken.None);
        session.CurrentQuality.Id.Should().Be(expected: "480p");

        // Third consecutive eligible sweep meets the hysteresis count — raises
        // exactly ONE tier up (720p), not straight to the bandwidth-fitting 1080p.
        await service.EvaluateAllAsync(ct: CancellationToken.None);
        await Task.Delay(millisecondsDelay: 50);
        session.CurrentQuality.Id.Should().Be(expected: "720p");
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

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // Old client never calls ReportClientBufferHealth — ClientBufferedAhead
        // and ObservedBandwidthKbps stay at their defaults, and
        // HasFreshClientHealth stays false, exactly as for a pre-upgrade client.
        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 40), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        BufferAdaptiveService service = BuildService(streamingService: streamingService);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Buffered);
    }

    [Fact]
    public async Task EvaluateAll_StaleClientHealthReport_FallsBackToEncoderLeadSuspend()
    {
        LiveStreamingService streamingService = NewStreamingService();

        LiveSession session = new(sessionId: Ulid.NewUlid().ToString(), quality: MakeQuality());
        session.SetState(state: LiveSessionState.Transcoding);
        streamingService.Register(session: session, targetSegmentDuration: TimeSpan.FromSeconds(seconds: 4));

        // A report landed once, but it is older than the staleness window by
        // the time the sweep runs — treated exactly like no report at all.
        session.ReportClientBufferHealth(bufferedAhead: TimeSpan.FromSeconds(seconds: 1), observedBandwidthKbps: 1000);
        await Task.Delay(millisecondsDelay: 50);

        session.PushSegment(segment: new(Index: 0, StartTime: TimeSpan.Zero, Duration: TimeSpan.FromSeconds(seconds: 40), FilePath: "/tmp/seg0.ts", SizeBytes: 1000));

        LiveSessionLimits limits = new();
        limits.Buffer.ClientHealthStalenessSeconds = 0;
        BufferAdaptiveService service = BuildService(streamingService: streamingService, limits: limits);

        await service.EvaluateAllAsync(ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Buffered);
    }
}
