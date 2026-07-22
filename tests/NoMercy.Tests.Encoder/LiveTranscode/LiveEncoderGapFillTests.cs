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

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

/// <summary>
/// End-to-end wiring proof for the seek-coverage fix: <see cref="LiveGapPlannerTests"/>
/// proves the planner in isolation and <see cref="LiveFfmpegArgumentBuilderStopPositionTests"/>
/// proves "-t" emission, but neither proves <see cref="LiveEncoder"/> actually
/// consults the on-disk segment inventory and feeds the plan into the runner it
/// spawns. These tests drive a real <see cref="LiveEncoder"/> + real
/// <see cref="LiveSegmentInventory"/> over a real scratch directory, capture the
/// exact <see cref="LiveRunInput"/> handed to the runner, and assert on it — if
/// this wiring were dropped, every other test in this slice would still pass and
/// the user's re-encode-on-seek bug would be unfixed.
/// </summary>
public class LiveEncoderGapFillTests
{
    private const int SegDur = 6;

    private static LiveQuality MakeQuality() =>
        new(
            Id: "1080p",
            Label: "1080p",
            Width: 1920,
            Height: 1080,
            Codec: VideoCodecType.H264,
            BitrateKbps: 8000,
            Encoder: "libx264",
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    private static MediaInfo MakeMedia(TimeSpan duration) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska,webm",
            Duration: duration,
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
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mp4", "mkv"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: false,
            Supports10Bit: false,
            MaxBitrateKbps: 0
        );

    private sealed class CapturingLiveFfmpegRunner : ILiveFfmpegRunner
    {
        private readonly ConcurrentQueue<LiveRunInput> _captured = new();

        public IReadOnlyList<LiveRunInput> Captured => [.. _captured];

        public Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct)
        {
            _captured.Enqueue(item: input);
            return Task.CompletedTask;
        }
    }

    private sealed record Fixture(
        LiveEncoder Encoder,
        CapturingLiveFfmpegRunner Runner,
        IStorage Storage,
        string CachePath
    );

    private static Fixture BuildFixture()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        ILiveSegmentInventory segmentInventory = TestStorageFactory.CreateSegmentInventory(storage: storage);
        string cachePath = Path.Combine(path1: Path.GetTempPath(), path2: $"nomercy-gapfill-{Guid.NewGuid():N}");

        EncoderOptions options = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
            LiveTranscodeCachePath = cachePath,
            DefaultSegmentDurationSeconds = SegDur,
        };

        Mock<ILiveQualitySelector> selectorMock = new();
        LiveQuality quality = MakeQuality();
        selectorMock
            .Setup(expression: s =>
                s.SelectOptimal(
                    It.IsAny<MediaInfo>(),
                    It.IsAny<ClientCapabilities>(),
                    It.IsAny<SpeedIndex>(),
                    It.IsAny<IResourceBudget>()
                )
            )
            .Returns(value: quality);

        SessionManager sessionManager = new(
            limits: new() { MaxConcurrentSessions = 100, MaxSessionsPerUser = 100 }
        );
        LiveStreamingService streamingService = new(
            logger: NullLogger<LiveStreamingService>.Instance,
            storage: storage,
            segmentInventory: segmentInventory
        );
        CapturingLiveFfmpegRunner runner = new();
        SpeedIndex speedIndex = new(Measurements: new());
        IResourceBudget budget = new ResourceBudget(gpuDevices: [], cpuCores: 8);

        LiveEncoder encoder = new(
            qualitySelector: selectorMock.Object,
            sessionManager: sessionManager,
            streamingService: streamingService,
            runner: runner,
            segmentInventory: segmentInventory,
            options: options,
            speedIndex: speedIndex,
            budget: budget,
            logger: NullLogger<LiveEncoder>.Instance
        );

        return new(Encoder: encoder, Runner: runner, Storage: storage, CachePath: cachePath);
    }

    private static string ScratchDirFor(Fixture fixture, string sessionId) =>
        Path.Combine(path1: fixture.CachePath, path2: $"lts-{sessionId}");

    private static void PlantSegments(
        Fixture fixture,
        string scratchDir,
        int fromIndex,
        int toIndexInclusive
    )
    {
        fixture.Storage.CreateDirectory(path: scratchDir);
        for (int index = fromIndex; index <= toIndexInclusive; index++)
        {
            string path = fixture.Storage.CombinePath(parent: scratchDir, child: $"seg_{index:D5}.ts");
            fixture.Storage.Write(path: path, bytes: [1]);
        }
    }

    private static async Task WaitForCaptureCountAsync(
        CapturingLiveFfmpegRunner runner,
        int expectedCount,
        TimeSpan timeout
    )
    {
        DateTime deadline = DateTime.UtcNow.Add(value: timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (runner.Captured.Count >= expectedCount)
                return;
            await Task.Delay(millisecondsDelay: 10);
        }
    }

    private static async Task WaitForStateAsync(
        ILiveSession session,
        LiveSessionState expected,
        TimeSpan timeout
    )
    {
        DateTime deadline = DateTime.UtcNow.Add(value: timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (session.State == expected)
                return;
            await Task.Delay(millisecondsDelay: 10);
        }
    }

    [Fact]
    public async Task SeekIntoGapBetweenTwoCoveredRanges_BoundsTheRespawn_InsteadOfReencodingToEof()
    {
        // The exact bug: covered 0..50 and 200..260, seek lands in the gap at
        // index 100. Without the fix the respawn is unbounded, re-encodes
        // 200..260 (content already on disk) and continues to EOF.
        Fixture fixture = BuildFixture();
        MediaInfo media = MakeMedia(duration: TimeSpan.FromHours(hours: 1)); // lastIndex far beyond 260

        LiveEncodeRequest request = new(
            InputPath: "/media/test.mkv",
            CachedInfo: media,
            Client: MakeClient(),
            StartPosition: TimeSpan.Zero,
            PreferredQuality: null
        );

        ILiveSession session = await fixture.Encoder.StartAsync(request: request, ct: CancellationToken.None);
        await WaitForCaptureCountAsync(runner: fixture.Runner, expectedCount: 1, timeout: TimeSpan.FromSeconds(seconds: 5));
        fixture
            .Runner.Captured.Should()
            .HaveCount(expected: 1, because: "the session-start spawn must have fired first");

        string scratchDir = ScratchDirFor(fixture: fixture, sessionId: session.SessionId);
        PlantSegments(fixture: fixture, scratchDir: scratchDir, fromIndex: 0, toIndexInclusive: 50);
        PlantSegments(fixture: fixture, scratchDir: scratchDir, fromIndex: 200, toIndexInclusive: 260);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 100 * SegDur), ct: CancellationToken.None);
        await WaitForCaptureCountAsync(runner: fixture.Runner, expectedCount: 2, timeout: TimeSpan.FromSeconds(seconds: 5));

        fixture.Runner.Captured.Should().HaveCount(expected: 2);
        LiveRunInput seekRun = fixture.Runner.Captured[index: 1];
        seekRun.StartPosition.Should().Be(expected: TimeSpan.FromSeconds(seconds: 100 * SegDur));
        seekRun.StopPosition.Should().Be(expected: TimeSpan.FromSeconds(seconds: 200 * SegDur));
    }

    [Fact]
    public async Task SeekIntoAlreadyCoveredGround_SkipsForwardToTheRealGap_AndRunsToEof()
    {
        // Covered 0..50, seek to index 20 (already covered) — the respawn must
        // skip forward to the first real gap (51), not re-encode 20..50.
        Fixture fixture = BuildFixture();
        MediaInfo media = MakeMedia(duration: TimeSpan.FromHours(hours: 1));

        LiveEncodeRequest request = new(
            InputPath: "/media/test.mkv",
            CachedInfo: media,
            Client: MakeClient(),
            StartPosition: TimeSpan.Zero,
            PreferredQuality: null
        );

        ILiveSession session = await fixture.Encoder.StartAsync(request: request, ct: CancellationToken.None);
        await WaitForCaptureCountAsync(runner: fixture.Runner, expectedCount: 1, timeout: TimeSpan.FromSeconds(seconds: 5));

        string scratchDir = ScratchDirFor(fixture: fixture, sessionId: session.SessionId);
        PlantSegments(fixture: fixture, scratchDir: scratchDir, fromIndex: 0, toIndexInclusive: 50);

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 20 * SegDur), ct: CancellationToken.None);
        await WaitForCaptureCountAsync(runner: fixture.Runner, expectedCount: 2, timeout: TimeSpan.FromSeconds(seconds: 5));

        fixture.Runner.Captured.Should().HaveCount(expected: 2);
        LiveRunInput seekRun = fixture.Runner.Captured[index: 1];
        seekRun.StartPosition.Should().Be(expected: TimeSpan.FromSeconds(seconds: 51 * SegDur));
        seekRun.StopPosition.Should().BeNull();
    }

    [Fact]
    public async Task SeekIntoFullyCoveredFile_SkipsTheSpawnEntirely_AndParksBuffered()
    {
        // The whole (short) file is already on disk — spawning would re-encode
        // content the client can already fetch. No new LiveRunInput must be
        // captured, and the session must park itself Buffered instead.
        Fixture fixture = BuildFixture();
        MediaInfo media = MakeMedia(duration: TimeSpan.FromSeconds(seconds: 60)); // lastIndex = 9 at 6s segments

        LiveEncodeRequest request = new(
            InputPath: "/media/test.mkv",
            CachedInfo: media,
            Client: MakeClient(),
            StartPosition: TimeSpan.Zero,
            PreferredQuality: null
        );

        ILiveSession session = await fixture.Encoder.StartAsync(request: request, ct: CancellationToken.None);
        await WaitForCaptureCountAsync(runner: fixture.Runner, expectedCount: 1, timeout: TimeSpan.FromSeconds(seconds: 5));

        string scratchDir = ScratchDirFor(fixture: fixture, sessionId: session.SessionId);
        PlantSegments(fixture: fixture, scratchDir: scratchDir, fromIndex: 0, toIndexInclusive: 9); // the entire file, 0..lastIndex

        await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 5 * SegDur), ct: CancellationToken.None);
        await WaitForStateAsync(session: session, expected: LiveSessionState.Buffered, timeout: TimeSpan.FromSeconds(seconds: 5));

        fixture
            .Runner.Captured.Should()
            .HaveCount(expected: 1, because: "a fully-covered seek must not spawn a new run");
        session.State.Should().Be(expected: LiveSessionState.Buffered);
    }
}
