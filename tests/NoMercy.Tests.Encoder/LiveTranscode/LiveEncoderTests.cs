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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveEncoderTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static MediaInfo MakeMedia() =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska,webm",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 5_000_000_000L,
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
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static ClientCapabilities MakeClient() =>
        new(
            SupportedVideoCodecs: [VideoCodecType.H264, VideoCodecType.H265],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mp4", "mkv"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: false,
            Supports10Bit: false,
            MaxBitrateKbps: 0
        );

    private static LiveQuality MakeQuality(string id = "1080p") =>
        new(
            Id: id,
            Label: id,
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static SpeedIndex MakeSpeedIndex() => new(Measurements: new());

    private static IResourceBudget MakeBudget() => new ResourceBudget(gpuDevices: [], cpuCores: 8);

    private static LiveEncodeRequest MakeRequest(string? preferredQuality = null) =>
        new(
            InputPath: "/media/test.mkv",
            CachedInfo: MakeMedia(),
            Client: MakeClient(),
            StartPosition: TimeSpan.Zero,
            PreferredQuality: preferredQuality
        );

    private LiveEncoder BuildEncoder(
        ISessionManager? sessionManager = null,
        ILiveQualitySelector? qualitySelector = null
    )
    {
        SpeedIndex speedIndex = MakeSpeedIndex();
        IResourceBudget budget = MakeBudget();
        LiveQuality defaultQuality = MakeQuality();

        ILiveQualitySelector selector =
            qualitySelector ?? CreateAlwaysReturnSelector(quality: defaultQuality);

        ISessionManager manager = sessionManager ?? CreateUnlimitedSessionManager();

        EncoderOptions encoderOptions = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
            LiveTranscodeCachePath = Path.Combine(path1: Path.GetTempPath(), path2: "nomercy-live-tests"),
        };

        NoMercy.Storage.IStorage storage = TestStorageFactory.CreateLocal();
        ILiveSegmentInventory segmentInventory = TestStorageFactory.CreateSegmentInventory(storage: storage);

        return new(
            qualitySelector: selector,
            sessionManager: manager,
            streamingService: new LiveStreamingService(
                logger: NullLogger<LiveStreamingService>.Instance,
                storage: storage,
                segmentInventory: segmentInventory
            ),
            runner: new NoOpLiveFfmpegRunner(),
            segmentInventory: segmentInventory,
            options: encoderOptions,
            speedIndex: speedIndex,
            budget: budget,
            logger: NullLogger<LiveEncoder>.Instance
        );
    }

    private sealed class NoOpLiveFfmpegRunner : ILiveFfmpegRunner
    {
        public Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct)
        {
            // Tests assert state + registration semantics — no real FFmpeg spawn.
            return Task.CompletedTask;
        }
    }

    private static ILiveQualitySelector CreateAlwaysReturnSelector(LiveQuality quality)
    {
        Mock<ILiveQualitySelector> mock = new();

        mock.Setup(expression: s =>
                s.SelectOptimal(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: quality);

        mock.Setup(expression: s =>
                s.GetAvailableQualities(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: [quality]);

        return mock.Object;
    }

    private static ISessionManager CreateUnlimitedSessionManager()
    {
        SessionManager manager = new(
            limits: new() { MaxConcurrentSessions = 100, MaxSessionsPerUser = 100 }
        );
        return manager;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StartAsync — happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsValidSession()
    {
        LiveEncoder encoder = BuildEncoder();
        LiveEncodeRequest request = MakeRequest();

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        session.Should().NotBeNull();
        session.SessionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StartAsync_SessionState_IsTranscoding()
    {
        LiveEncoder encoder = BuildEncoder();
        LiveEncodeRequest request = MakeRequest();

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        session.State.Should().Be(expected: LiveSessionState.Transcoding);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // StartAudioRenditionAsync — per-language audio child
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAudioRenditionAsync_StartsChild_BypassingTheSessionCap()
    {
        // Cap of zero: a normal StartAsync would be refused, but an audio child
        // rides on an already-admitted video session and must not be capped.
        SessionManager capped = new(limits: new() { MaxConcurrentSessions = 0, MaxSessionsPerUser = 0 });
        LiveEncoder encoder = BuildEncoder(sessionManager: capped);
        LiveEncodeRequest request = MakeRequest() with { AudioStreamIndex = 2 };

        ILiveSession child = await encoder.StartAudioRenditionAsync(
            request: request,
            ct: CancellationToken.None
        );

        child.Should().NotBeNull();
        child.State.Should().Be(expected: LiveSessionState.Transcoding);
        child.CurrentAudioStreamIndex.Should().Be(expected: 2);
    }

    [Fact]
    public async Task StartAsync_WhenCapExhausted_Throws()
    {
        SessionManager capped = new(limits: new() { MaxConcurrentSessions = 0, MaxSessionsPerUser = 0 });
        LiveEncoder encoder = BuildEncoder(sessionManager: capped);

        Func<Task> act = () => encoder.StartAsync(request: MakeRequest(), ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_UsesOptimalQuality_WhenNoPreference()
    {
        LiveQuality expectedQuality = MakeQuality(id: "720p");
        LiveEncoder encoder = BuildEncoder(
            qualitySelector: CreateAlwaysReturnSelector(quality: expectedQuality)
        );
        LiveEncodeRequest request = MakeRequest(preferredQuality: null);

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        session.CurrentQuality.Id.Should().Be(expected: "720p");
    }

    [Fact]
    public async Task StartAsync_WithPreferredQuality_UsesItWhenAvailable()
    {
        LiveQuality preferred = MakeQuality();
        LiveQuality fallback = MakeQuality(id: "720p");

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
            .Returns(value: [preferred]);

        selectorMock
            .Setup(expression: s =>
                s.SelectOptimal(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: fallback);

        LiveEncoder encoder = BuildEncoder(qualitySelector: selectorMock.Object);
        LiveEncodeRequest request = MakeRequest(preferredQuality: "1080p");

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        session.CurrentQuality.Id.Should().Be(expected: "1080p");
    }

    [Fact]
    public async Task StartAsync_WithPreferredQualityNotAvailable_FallsBackToOptimal()
    {
        LiveQuality available = MakeQuality(id: "480p");
        LiveQuality optimal = MakeQuality(id: "720p");

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
            .Returns(value: [available]);

        selectorMock
            .Setup(expression: s =>
                s.SelectOptimal(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: optimal);

        LiveEncoder encoder = BuildEncoder(qualitySelector: selectorMock.Object);
        LiveEncodeRequest request = MakeRequest(preferredQuality: "4k");

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        session.CurrentQuality.Id.Should().Be(expected: "720p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Session registration
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_DoesNotRegisterSessionInSessionManager()
    {
        SessionManager sessionManager = new(limits: new() { MaxConcurrentSessions = 10 });
        LiveEncoder encoder = BuildEncoder(sessionManager: sessionManager);
        LiveEncodeRequest request = MakeRequest();

        await encoder.StartAsync(request: request, ct: CancellationToken.None);

        sessionManager.ActiveSessionCount.Should().Be(expected: 0);
    }

    [Fact]
    public async Task StartAsync_EachCallProducesUniqueSessionId()
    {
        LiveEncoder encoder = BuildEncoder();

        ILiveSession first = await encoder.StartAsync(request: MakeRequest(), ct: CancellationToken.None);
        ILiveSession second = await encoder.StartAsync(request: MakeRequest(), ct: CancellationToken.None);

        first.SessionId.Should().NotBe(unexpected: second.SessionId);
    }

    [Fact]
    public async Task StartAsync_ControllerRegistersExactlyOnce_WhenControllerCallsRegister()
    {
        SessionManager sessionManager = new(limits: new() { MaxConcurrentSessions = 10 });
        LiveEncoder encoder = BuildEncoder(sessionManager: sessionManager);
        LiveEncodeRequest request = MakeRequest();

        ILiveSession session = await encoder.StartAsync(request: request, ct: CancellationToken.None);

        // Encoder must NOT have registered — count stays 0.
        sessionManager.ActiveSessionCount.Should().Be(expected: 0);

        // Controller registers once with userId.
        sessionManager.RegisterSession(session: session, userId: "user-abc");

        // Now exactly one registration exists.
        sessionManager.ActiveSessionCount.Should().Be(expected: 1);
        sessionManager.ActiveSessions.Should().ContainSingle(predicate: s => s.SessionId == session.SessionId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Session limit enforcement
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ThrowsWhenSessionLimitReached()
    {
        SessionManager sessionManager = new(limits: new() { MaxConcurrentSessions = 1 });
        LiveEncoder encoder = BuildEncoder(sessionManager: sessionManager);

        // Simulate the controller having registered a session against the cap.
        LiveSession existing = new(sessionId: "existing-session", quality: MakeQuality());
        sessionManager.RegisterSession(session: existing, userId: "user-1");

        Func<Task> act = () => encoder.StartAsync(request: MakeRequest(), ct: CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*concurrent*");
    }
}
