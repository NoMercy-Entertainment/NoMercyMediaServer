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
using NoMercy.Resources;
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
            Duration: TimeSpan.FromMinutes(90),
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

    private static BufferAdaptiveService BuildService(
        ILiveStreamingService streamingService,
        ILiveQualitySelector? qualitySelector = null
    )
    {
        BufferManager bufferManager = new(new LiveSessionLimits());
        SpeedIndex speedIndex = new(new());
        Mock<IResourceBudget> budgetMock = new();

        ILiveQualitySelector selector = qualitySelector ?? Mock.Of<ILiveQualitySelector>();

        return new BufferAdaptiveService(
            streamingService,
            selector,
            bufferManager,
            speedIndex,
            budgetMock.Object,
            NullLogger<BufferAdaptiveService>.Instance
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Suspend: over-buffered session gets suspended
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAll_OverBufferedSession_GetsSuspended()
    {
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Simulate 40 s transcoded, 0 s reported — buffer = 40 s (above suspend threshold of 30)
        session.PushSegment(
            new Segment(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000)
        );

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
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Buffered);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Simulate 10 s transcoded, 0 s reported — buffer = 10 s (below resume threshold of 15)
        session.PushSegment(
            new Segment(0, TimeSpan.Zero, TimeSpan.FromSeconds(10), "/tmp/seg0.ts", 1000)
        );

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
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

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
        session.PushSegment(
            new Segment(0, TimeSpan.Zero, TimeSpan.FromSeconds(4), "/tmp/seg0.ts", 1000)
        );
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
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // 20 s buffered — within the healthy range
        session.PushSegment(
            new Segment(0, TimeSpan.Zero, TimeSpan.FromSeconds(20), "/tmp/seg0.ts", 1000)
        );

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
        LiveStreamingService streamingService = new(
            NullLogger<LiveStreamingService>.Instance,
            TestStorageFactory.CreateLocal()
        );

        LiveSession session = new(Ulid.NewUlid().ToString(), MakeQuality());
        session.SetState(LiveSessionState.Transcoding);
        streamingService.Register(session, TimeSpan.FromSeconds(4));

        // Mark as complete so it drains
        session.PushSegment(
            new Segment(0, TimeSpan.Zero, TimeSpan.FromSeconds(40), "/tmp/seg0.ts", 1000)
        );
        session.Complete();

        if (streamingService.TryGetRuntime(session.SessionId, out LiveRuntimeSession runtime))
            runtime.MarkComplete();

        BufferAdaptiveService service = BuildService(streamingService);

        await service.EvaluateAllAsync(CancellationToken.None);

        // Complete sessions are skipped — state should not have been flipped to Buffered
        // even though buffer is way ahead (the session is done)
        session.State.Should().NotBe(LiveSessionState.Buffered);
    }
}
