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
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveFfmpegRunnerTests
{
    private static INvencSessionCap NoopCap()
    {
        Mock<INvencSessionCap> m = new();
        return m.Object;
    }

    private static IHardwareCapabilities NoopHardware() =>
        new HardwareCapabilities(Gpus: [], CpuCores: Environment.ProcessorCount);

    private static IResourceBudget NoopBudget()
    {
        Mock<IResourceBudget> mock = new();
        ResourceLease lease = new(LeaseId: "noop", GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 0);
        mock.Setup(expression: b => b.Acquire(It.IsAny<ResourceRequirement>())).Returns(value: lease);
        mock.Setup(expression: b =>
                b.AcquireAsync(It.IsAny<ResourceRequirement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: lease);
        mock.Setup(expression: b => b.Release(It.IsAny<ResourceLease>()));
        return mock.Object;
    }

    private static ICodecResolver RealCodecResolver() => new CodecResolver(registry: new CodecRegistry());

    private static LiveFfmpegRunner MakeRunner(IProcessRunner? processRunner = null) =>
        new(
            processRunner: processRunner ?? new FakeProcessRunner(onRun: () => { }),
            options: new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            logger: NullLogger<LiveFfmpegRunner>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            nvencSessionCap: NoopCap(),
            hardware: NoopHardware(),
            codecResolver: RealCodecResolver(),
            resourceBudget: NoopBudget()
        );

    private static LiveQuality MakeQuality(
        int width = 1920,
        int height = 1080,
        int kbps = 8000,
        string encoder = "libx264"
    ) =>
        new(
            Id: $"{height}p",
            Label: $"{height}p",
            Width: width,
            Height: height,
            Codec: VideoCodecType.H264,
            BitrateKbps: kbps,
            Encoder: encoder,
            IsHardwareAccelerated: false,
            ExpectedSpeed: 2.0,
            CanRealtime: true
        );

    // ──────────────────────────────────────────────────────────────────────────
    // BuildArguments
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_IncludesCoreHlsFlags()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        args.Should().Contain(expected: "-f").And.Contain(expected: "hls");
        args.Should().Contain(expected: "-hls_time");
        args[Array.IndexOf(array: args, value: "-hls_time") + 1].Should().Be(expected: "4");
        args.Should().Contain(expected: "-hls_list_size");
        args[Array.IndexOf(array: args, value: "-hls_list_size") + 1].Should().Be(expected: "0");
        args.Should().Contain(expected: "-hls_playlist_type");
        args[Array.IndexOf(array: args, value: "-hls_playlist_type") + 1].Should().Be(expected: "event");
        args.Should().Contain(predicate: arg => arg.EndsWith("index.m3u8"));
    }

    [Fact]
    public void BuildArguments_OmitsSeek_WhenStartPositionIsZero()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        args.Should().NotContain(unexpected: "-ss");
        // The first run still numbers its segments from zero.
        args.Should().Contain(expected: "-start_number");
        args[Array.IndexOf(array: args, value: "-start_number") + 1].Should().Be(expected: "0");
    }

    [Fact]
    public void BuildArguments_SeekSnapsToSegmentBoundary_AndNumbersFromThatIndex()
    {
        // 120.5s with 6s segments falls inside absolute segment 20 (120–126s).
        // The input seek snaps to that segment's start and the muxer numbers its
        // first output segment 20, so seg_00020.ts maps 1:1 to the index hls.js
        // requests from the whole-runtime playlist. hls.js resolves the 0.5s
        // sub-segment offset by seeking inside the segment.
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.FromSeconds(value: 120.5),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int ssIdx = Array.IndexOf(array: args, value: "-ss");
        ssIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[ssIdx + 1].Should().Be(expected: "120.000");

        int startNumberIdx = Array.IndexOf(array: args, value: "-start_number");
        startNumberIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[startNumberIdx + 1].Should().Be(expected: "20");

        // Muxed PTS is shifted to the segment's true start so hls.js places the
        // seek fragment at 120s (20×6) instead of 0.
        int offsetIdx = Array.IndexOf(array: args, value: "-output_ts_offset");
        offsetIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[offsetIdx + 1].Should().Be(expected: "120.000");
    }

    [Fact]
    public void BuildArguments_UsesQualityEncoderAndBitrate()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(kbps: 3500, encoder: "h264_nvenc"),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vCodecIdx = Array.IndexOf(array: args, value: "-c:v");
        args[vCodecIdx + 1].Should().Be(expected: "h264_nvenc");

        int bvIdx = Array.IndexOf(array: args, value: "-b:v");
        args[bvIdx + 1].Should().Be(expected: "3500k");
    }

    [Fact]
    public void BuildArguments_UsesScaleFilterFromQualityDimensions()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(width: 1280, height: 720, kbps: 4000),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vfIdx = Array.IndexOf(array: args, value: "-vf");
        args[vfIdx + 1].Should().Be(expected: "scale=1280:720,format=yuv420p");
    }

    [Fact]
    public void BuildArguments_IncludesProgressPipe_ForSpeedTracking()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int progIdx = Array.IndexOf(array: args, value: "-progress");
        progIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[progIdx + 1].Should().Be(expected: "pipe:1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ParsePlaylist
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsePlaylist_ExtractsDurationAndIndexFromEachEntry()
    {
        string path = WritePlaylist(
            content: """
                     #EXTM3U
                     #EXT-X-VERSION:3
                     #EXT-X-TARGETDURATION:6
                     #EXT-X-MEDIA-SEQUENCE:0
                     #EXTINF:6.000000,
                     seg_00000.ts
                     #EXTINF:6.000000,
                     seg_00001.ts
                     #EXTINF:4.040000,
                     seg_00002.ts
                     """
        );

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = MakeRunner().ParsePlaylist(playlistPath: path);

        entries.Should().HaveCount(expected: 3);
        entries[index: 0].Index.Should().Be(expected: 0);
        entries[index: 0].Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 6));
        entries[index: 1].Index.Should().Be(expected: 1);
        entries[index: 2].Index.Should().Be(expected: 2);
        entries[index: 2].Duration.TotalSeconds.Should().BeApproximately(expectedValue: 4.04, precision: 0.001);
    }

    [Fact]
    public void ParsePlaylist_IgnoresComments_AndEndlist()
    {
        string path = WritePlaylist(
            content: """
                     #EXTM3U
                     #EXT-X-VERSION:3
                     #EXT-X-TARGETDURATION:6
                     #EXT-X-MEDIA-SEQUENCE:0
                     #EXTINF:6.000000,
                     seg_00000.ts
                     #EXT-X-ENDLIST
                     """
        );

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = MakeRunner().ParsePlaylist(playlistPath: path);

        entries.Should().HaveCount(expected: 1);
        entries[index: 0].Index.Should().Be(expected: 0);
    }

    [Fact]
    public void ParsePlaylist_MissingFile_ReturnsEmpty()
    {
        IReadOnlyList<(int Index, TimeSpan Duration)> entries = MakeRunner()
            .ParsePlaylist(
                playlistPath: Path.Combine(path1: Path.GetTempPath(), path2: Guid.NewGuid().ToString(), path3: "index.m3u8")
            );

        entries.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RunAsync — integration with fake process runner + playlist file
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PushesSegmentsProducedDuringRun()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"live-runner-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            LiveRunInput input = new(
                InputPath: "/media/in.mkv",
                OutputDirectory: tempDir,
                StartPosition: TimeSpan.Zero,
                Quality: MakeQuality(),
                SegmentDurationSeconds: 6
            );

            FakeProcessRunner runner = new(onRun: () =>
            {
                // Simulate FFmpeg producing two segments during its run
                File.WriteAllText(path: Path.Combine(path1: tempDir, path2: "seg_00000.ts"), contents: new(c: 'a', count: 100));
                File.WriteAllText(path: Path.Combine(path1: tempDir, path2: "seg_00001.ts"), contents: new(c: 'a', count: 100));
                File.WriteAllText(
                    path: Path.Combine(path1: tempDir, path2: "index.m3u8"),
                    contents: """
                              #EXTM3U
                              #EXT-X-VERSION:3
                              #EXT-X-TARGETDURATION:6
                              #EXT-X-MEDIA-SEQUENCE:0
                              #EXTINF:6.000000,
                              seg_00000.ts
                              #EXTINF:6.000000,
                              seg_00001.ts
                              #EXT-X-ENDLIST
                              """
                );
            });

            LiveFfmpegRunner sut = new(
                processRunner: runner,
                options: new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
                logger: NullLogger<LiveFfmpegRunner>.Instance,
                storage: TestStorageFactory.CreateLocal(),
                nvencSessionCap: NoopCap(),
                hardware: NoopHardware(),
                codecResolver: RealCodecResolver(),
                resourceBudget: NoopBudget()
            );

            LiveSession session = new(sessionId: "sess", quality: MakeQuality());
            session.SetState(state: LiveSessionState.Transcoding);

            await sut.RunAsync(input: input, session: session, ct: CancellationToken.None);

            List<Segment> received = [];
            await foreach (Segment segment in session.Segments)
            {
                received.Add(item: segment);
                if (received.Count >= 2)
                    break;
            }

            received.Should().HaveCount(expected: 2);
            received.Select(selector: s => s.Index).Should().Equal(elements: [0, 1]);
        }
        finally
        {
            try
            {
                Directory.Delete(path: tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Conditional session-complete: a superseded (per-seek) runner must not
    // end the whole session's segment stream when its own ffmpeg exits.
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task DrainAsync(LiveSession session)
    {
        await foreach (Segment _ in session.Segments) { }
    }

    [Fact]
    public async Task RunAsync_StillCurrentRunner_CompletesSessionSegmentChannel()
    {
        string tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"live-runner-current-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: tempDir);

        try
        {
            LiveRunInput input = new(
                InputPath: "/media/in.mkv",
                OutputDirectory: tempDir,
                StartPosition: TimeSpan.Zero,
                Quality: MakeQuality(),
                SegmentDurationSeconds: 6
            );

            LiveFfmpegRunner sut = MakeRunner(processRunner: new FakeProcessRunner(onRun: () => { }));
            LiveSession session = new(sessionId: "sess-current", quality: MakeQuality());
            session.SetState(state: LiveSessionState.Transcoding);

            // RunAsync invoked with the session's OWN current runner token —
            // mirrors the real _runnerFactory wiring (LiveEncoder.SpawnRunner)
            // where no newer seek has superseded this runner.
            await sut.RunAsync(input: input, session: session, ct: session.RunnerCancellation);

            Task drain = DrainAsync(session: session);
            Task completed = await Task.WhenAny(task1: drain, task2: Task.Delay(delay: TimeSpan.FromSeconds(seconds: 2)));

            completed.Should().Be(expected: drain, because: "the current runner must complete the segment channel");
        }
        finally
        {
            try
            {
                Directory.Delete(path: tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task RunAsync_SupersededBySeek_DoesNotCompleteSessionSegmentChannel()
    {
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"live-runner-stale-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: tempDir);

        try
        {
            LiveRunInput input = new(
                InputPath: "/media/in.mkv",
                OutputDirectory: tempDir,
                StartPosition: TimeSpan.Zero,
                Quality: MakeQuality(),
                SegmentDurationSeconds: 6
            );

            LiveFfmpegRunner sut = MakeRunner(processRunner: new FakeProcessRunner(onRun: () => { }));
            LiveSession session = new(sessionId: "sess-stale", quality: MakeQuality());
            session.SetState(state: LiveSessionState.Transcoding);

            CancellationToken staleToken = session.RunnerCancellation;

            // A seek/resume/quality-change swaps in a fresh runner CTS before
            // this (now-superseded) runner's ffmpeg process finishes. No
            // factory is attached, so the seek only replaces the CTS.
            await session.SeekAsync(position: TimeSpan.FromSeconds(seconds: 10), ct: CancellationToken.None);

            await sut.RunAsync(input: input, session: session, ct: staleToken);

            Task drain = DrainAsync(session: session);
            Task completed = await Task.WhenAny(task1: drain, task2: Task.Delay(delay: TimeSpan.FromMilliseconds(milliseconds: 400)));

            completed
                .Should()
                .NotBe(unexpected: drain, because: "a superseded runner must not end the session's segment stream");
        }
        finally
        {
            try
            {
                Directory.Delete(path: tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resource-lease lifetime: the lease is acquired before CreateDirectory /
    // AcquireLocalPath / BuildArguments run — a throw from any of those must
    // still release it, not leak the GPU/CPU budget forever.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ThrowFromCreateDirectory_StillReleasesResourceLease()
    {
        Mock<IStorage> storage = new();
        storage
            .Setup(expression: s => s.CreateDirectory(It.IsAny<string>()))
            .Throws(exception: new IOException(message: "disk full"));

        ResourceLease lease = new(LeaseId: "lease-1", GpuDeviceKey: null, GpuSlots: 0, CpuThreads: 2);
        Mock<IResourceBudget> budget = new();
        budget
            .Setup(expression: b =>
                b.AcquireAsync(It.IsAny<ResourceRequirement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: lease);

        LiveFfmpegRunner sut = new(
            processRunner: new FakeProcessRunner(onRun: () => { }),
            options: new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            logger: NullLogger<LiveFfmpegRunner>.Instance,
            storage: storage.Object,
            nvencSessionCap: NoopCap(),
            hardware: NoopHardware(),
            codecResolver: RealCodecResolver(),
            resourceBudget: budget.Object
        );

        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );
        LiveSession session = new(sessionId: "sess-throw", quality: MakeQuality());

        Func<Task> act = () => sut.RunAsync(input: input, session: session, ct: CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        budget.Verify(expression: b => b.Release(lease), times: Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Client capabilities: audio channels + HDR tonemap
    // ──────────────────────────────────────────────────────────────────────────

    private static ClientCapabilities MakeClientCaps(bool supportsHdr, int maxAudioChannels) =>
        new(
            SupportedVideoCodecs: [],
            SupportedAudioCodecs: [],
            SupportedContainers: [],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: supportsHdr,
            Supports10Bit: false,
            MaxBitrateKbps: 8000,
            MaxAudioChannels: maxAudioChannels
        );

    private static MediaInfo MakeMediaInfo(bool isHdr, int audioChannels)
    {
        VideoStreamInfo video = new(
            Index: 0,
            Codec: "hevc",
            Width: 3840,
            Height: 2160,
            FrameRate: 24,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: isHdr ? "bt2020" : "bt709",
            ColorTransfer: isHdr ? "smpte2084" : "bt709",
            ColorSpace: isHdr ? "bt2020nc" : "bt709",
            IsDefault: true,
            BitRateKbps: 40000
        );
        AudioStreamInfo audio = new(
            Index: 1,
            Codec: "truehd",
            Channels: audioChannels,
            SampleRate: 48000,
            BitRateKbps: 3000,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );
        return new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 120),
            OverallBitRateKbps: 43000,
            FileSizeBytes: 40_000_000_000L,
            VideoStreams: [video],
            AudioStreams: [audio],
            SubtitleStreams: [],
            Chapters: []
        );
    }

    [Fact]
    public void BuildArguments_HdrSource_SdrClient_AppendsTonemap()
    {
        // HDR source, client doesn't support HDR → tonemap chain must appear.
        LiveRunInput input = new(
            InputPath: "/media/hdr.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            Client: MakeClientCaps(supportsHdr: false, maxAudioChannels: 2),
            SourceInfo: MakeMediaInfo(isHdr: true, audioChannels: 2)
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vfIdx = Array.IndexOf(array: args, value: "-vf");
        string vf = args[vfIdx + 1];
        vf.Should().Contain(expected: "scale=");
        vf.Should().Contain(expected: "zscale=t=linear");
        vf.Should().Contain(expected: "tonemap=tonemap=hable");
        vf.Should().Contain(expected: "format=yuv420p");
    }

    [Fact]
    public void BuildArguments_HdrSource_HdrClient_NoTonemap()
    {
        // HDR source, client supports HDR → no tonemap.
        LiveRunInput input = new(
            InputPath: "/media/hdr.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            Client: MakeClientCaps(supportsHdr: true, maxAudioChannels: 8),
            SourceInfo: MakeMediaInfo(isHdr: true, audioChannels: 8)
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vfIdx = Array.IndexOf(array: args, value: "-vf");
        string vf = args[vfIdx + 1];
        vf.Should().NotContain(unexpected: "zscale");
        vf.Should().NotContain(unexpected: "tonemap");
    }

    [Fact]
    public void BuildArguments_AudioChannels_CappedByClientMax()
    {
        // Source has 7.1 (8 ch), client claims max 2 → -ac 2.
        LiveRunInput input = new(
            InputPath: "/media/surround.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            Client: MakeClientCaps(supportsHdr: false, maxAudioChannels: 2),
            SourceInfo: MakeMediaInfo(isHdr: false, audioChannels: 8)
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int acIdx = Array.IndexOf(array: args, value: "-ac");
        args[acIdx + 1].Should().Be(expected: "2");
    }

    [Fact]
    public void BuildArguments_AudioChannels_UsesSourceWhenBelowClientMax()
    {
        // Source has stereo (2 ch), client claims max 8 → -ac 2 (don't upmix).
        LiveRunInput input = new(
            InputPath: "/media/stereo.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            Client: MakeClientCaps(supportsHdr: false, maxAudioChannels: 8),
            SourceInfo: MakeMediaInfo(isHdr: false, audioChannels: 2)
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int acIdx = Array.IndexOf(array: args, value: "-ac");
        args[acIdx + 1].Should().Be(expected: "2");
    }

    [Fact]
    public void BuildArguments_NoClientCaps_DefaultsToStereo()
    {
        // No client caps → fallback to -ac 2.
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int acIdx = Array.IndexOf(array: args, value: "-ac");
        args[acIdx + 1].Should().Be(expected: "2");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Browser-safe 8-bit pixel format (2026-07 fix)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_TenBitSdrSource_ForcesYuv420p()
    {
        // 10-bit HEVC SDR source transcoded to H.264 for a browser client.
        // Before this fix, the non-tonemap path never forced a pixel format,
        // so libx264 auto-negotiated High-10 — a profile no browser decodes.
        LiveRunInput input = new(
            InputPath: "/media/sdr10bit.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(encoder: "libx264"),
            SegmentDurationSeconds: 4,
            Client: MakeClientCaps(supportsHdr: false, maxAudioChannels: 2),
            SourceInfo: MakeMediaInfo(isHdr: false, audioChannels: 2)
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vfIdx = Array.IndexOf(array: args, value: "-vf");
        string vf = args[vfIdx + 1];
        vf.Should().NotContain(unexpected: "zscale");
        vf.Should().NotContain(unexpected: "tonemap");
        vf.Should().Contain(expected: "format=yuv420p");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GOP / keyframe alignment to HLS segment duration
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_Gop_ForceKeyFramesAndGopSizeTiedToSegmentDuration()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            SourceInfo: MakeMediaInfo(isHdr: false, audioChannels: 2) // FrameRate: 24
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int fkfIdx = Array.IndexOf(array: args, value: "-force_key_frames");
        fkfIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[fkfIdx + 1].Should().Be(expected: "expr:gte(t,n_forced*4)");

        int gIdx = Array.IndexOf(array: args, value: "-g");
        gIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[gIdx + 1].Should().Be(expected: "192"); // 24fps * 4s segment * 2
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Rate control + preset
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_RateControlAndPreset_PresentForFullTranscode()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(kbps: 4000, encoder: "libx264"),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int maxrateIdx = Array.IndexOf(array: args, value: "-maxrate");
        maxrateIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[maxrateIdx + 1].Should().Be(expected: "6000k");

        int bufsizeIdx = Array.IndexOf(array: args, value: "-bufsize");
        bufsizeIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[bufsizeIdx + 1].Should().Be(expected: "8000k");

        int presetIdx = Array.IndexOf(array: args, value: "-preset");
        presetIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[presetIdx + 1].Should().Be(expected: "veryfast");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Audio-only sources
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_AudioOnlySource_OmitsVideoMapAndAddsVn()
    {
        AudioStreamInfo audio = new(
            Index: 0,
            Codec: "flac",
            Channels: 2,
            SampleRate: 44100,
            BitRateKbps: 900,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );
        MediaInfo source = new(
            FilePath: "/media/audio-only.flac",
            Format: "flac",
            Duration: TimeSpan.FromMinutes(minutes: 4),
            OverallBitRateKbps: 900,
            FileSizeBytes: 30_000_000,
            VideoStreams: [],
            AudioStreams: [audio],
            SubtitleStreams: [],
            Chapters: []
        );

        LiveRunInput input = new(
            InputPath: "/media/audio-only.flac",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            SourceInfo: source
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        args.Should().NotContain(unexpected: "0:v:0");
        args.Should().Contain(expected: "-vn");
        int caIdx = Array.IndexOf(array: args, value: "-c:a");
        args[caIdx + 1].Should().Be(expected: "aac");
        args.Should().Contain(expected: "-f").And.Contain(expected: "hls");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // VideoOnly — audio comes from the file's own renditions via the master
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_VideoOnly_DropsAudioEntirely()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            AudioStreamIndex: 1,
            VideoOnly: true
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        // Video is still mapped and encoded, but audio is dropped: no audio map,
        // no audio codec — just "-an". The master playlist supplies the audio.
        args.Should().Contain(expected: "0:v:0");
        args.Should().Contain(expected: "-an");
        args.Should().NotContain(unexpected: "-c:a");
        args.Should().NotContain(predicate: a => a.StartsWith("0:a:"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AudioRenditionOnly — one language transcoded to AAC, no video
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_AudioRenditionOnly_MapsOneLanguageToAac_NoVideo()
    {
        LiveRunInput input = new(
            InputPath: "/media/remux.mkv",
            OutputDirectory: "/tmp/live-audio-jpn",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(),
            SegmentDurationSeconds: 4,
            AudioStreamIndex: 2,
            AudioRenditionOnly: true
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        // No video is produced or mapped, and the selected language is transcoded
        // to AAC — this is the per-language rendition for a raw multi-audio source.
        args.Should().Contain(expected: "-vn");
        args.Should().NotContain(unexpected: "0:v:0");
        args.Should().NotContain(unexpected: "-c:v");
        args.Should().Contain(expected: "-map").And.Contain(expected: "0:a:2?");
        int caIdx = Array.IndexOf(array: args, value: "-c:a");
        args[caIdx + 1].Should().Be(expected: "aac");
        // Still an HLS output so it can be served and seeked like the video track.
        args.Should().Contain(expected: "-f").And.Contain(expected: "hls");
    }

    [Fact]
    public void BuildArguments_AudioRenditionOnly_SharesAbsoluteSegmentIndexingWithVideo()
    {
        // A language rendition must seek to the same segment boundaries the video
        // does so hls.js keeps audio and video aligned across a seek.
        LiveRunInput input = new(
            InputPath: "/media/remux.mkv",
            OutputDirectory: "/tmp/live-audio-eng",
            StartPosition: TimeSpan.FromSeconds(value: 120.5),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6,
            AudioStreamIndex: 0,
            AudioRenditionOnly: true
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        args[Array.IndexOf(array: args, value: "-ss") + 1].Should().Be(expected: "120.000");
        args[Array.IndexOf(array: args, value: "-start_number") + 1].Should().Be(expected: "20");
        args[Array.IndexOf(array: args, value: "-output_ts_offset") + 1].Should().Be(expected: "120.000");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Remux vs full video transcode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_RemuxDecision_UsesStreamCopyForVideoAndAudio()
    {
        VideoStreamInfo video = new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 4500
        );
        AudioStreamInfo audio = new(
            Index: 1,
            Codec: "aac",
            Channels: 2,
            SampleRate: 48000,
            BitRateKbps: 128,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );
        MediaInfo source = new(
            FilePath: "/media/remux.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 5000,
            FileSizeBytes: 1_000_000_000,
            VideoStreams: [video],
            AudioStreams: [audio],
            SubtitleStreams: [],
            Chapters: []
        );
        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mp4"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0,
            MaxAudioChannels: 2
        );

        LiveRunInput input = new(
            InputPath: "/media/remux.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(encoder: "libx264"),
            SegmentDurationSeconds: 4,
            Client: client,
            SourceInfo: source
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vIdx = Array.IndexOf(array: args, value: "-c:v");
        args[vIdx + 1].Should().Be(expected: "copy");
        int aIdx = Array.IndexOf(array: args, value: "-c:a");
        args[aIdx + 1].Should().Be(expected: "copy");
        args.Should().NotContain(unexpected: "-preset");
        args.Should().NotContain(unexpected: "-maxrate");
        args.Should().NotContain(predicate: a => a.StartsWith("scale="));
    }

    [Fact]
    public void BuildArguments_TranscodeVideoDecision_ReEncodesVideo()
    {
        // Client doesn't support the source's video codec at all → TranscodeVideo,
        // full re-encode using the resolved quality's encoder — never a copy.
        VideoStreamInfo video = new(
            Index: 0,
            Codec: "hevc",
            Width: 1920,
            Height: 1080,
            FrameRate: 24,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 4500
        );
        MediaInfo source = new(
            FilePath: "/media/transcode.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 90),
            OverallBitRateKbps: 4500,
            FileSizeBytes: 1_000_000_000,
            VideoStreams: [video],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );
        ClientCapabilities client = new(
            SupportedVideoCodecs: [VideoCodecType.H264],
            SupportedAudioCodecs: [],
            SupportedContainers: ["mp4"],
            MaxWidth: 1920,
            MaxHeight: 1080,
            SupportsHdr: true,
            Supports10Bit: true,
            MaxBitrateKbps: 0,
            MaxAudioChannels: 2
        );

        LiveRunInput input = new(
            InputPath: "/media/transcode.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(encoder: "libx264"),
            SegmentDurationSeconds: 4,
            Client: client,
            SourceInfo: source
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int vIdx = Array.IndexOf(array: args, value: "-c:v");
        args[vIdx + 1].Should().Be(expected: "libx264");
        args.Should().Contain(predicate: a => a.StartsWith("scale="));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CustomArguments escape hatch
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildArguments_CustomArguments_AppearInFinalArgv_ButReservedFlagsAreSkipped()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(kbps: 4000, encoder: "libx264"),
            SegmentDurationSeconds: 4,
            CustomArguments: new Dictionary<string, string>
            {
                [key: "-x264-params"] = "nal-hrd=cbr",
                [key: "-b:v"] = "999k", // reserved — must be skipped, not override the resolved -b:v
            }
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input: input);

        int customIdx = Array.IndexOf(array: args, value: "-x264-params");
        customIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[customIdx + 1].Should().Be(expected: "nal-hrd=cbr");

        int bvIdx = Array.IndexOf(array: args, value: "-b:v");
        args[bvIdx + 1].Should().Be(expected: "4000k");
        args.Should().NotContain(unexpected: "999k");
    }

    private static string WritePlaylist(string content)
    {
        string path = Path.Combine(path1: Path.GetTempPath(), path2: $"live-pl-{Guid.NewGuid():N}.m3u8");
        File.WriteAllText(path: path, contents: content.Replace(oldValue: "\r\n", newValue: "\n"));
        return path;
    }

    private sealed class FakeProcessRunner(Action onRun) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string executable,
            string[] arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<ProcessResult> RunAsync(
            string executable,
            string[] arguments,
            Action<string>? onStdOut = null,
            Action<string>? onStdErr = null,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default
        )
        {
            onRun();
            return Task.FromResult(
                result: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        }

        public Task<ProcessResult> RunAsync(
            string executable,
            string[] arguments,
            Action<string>? onStdOut,
            Action<string>? onStdErr,
            string? workingDirectory,
            CancellationToken cancellationToken,
            CancellationToken killSignal,
            Action<int>? onProcessStarted = null
        )
        {
            onRun();
            return Task.FromResult(
                result: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        }

        public Task<ProcessResult> RunAsync(
            string executable,
            string[] arguments,
            IReadOnlyDictionary<string, string>? extraEnv,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                result: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
    }
}
