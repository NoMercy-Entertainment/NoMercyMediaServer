namespace NoMercy.Tests.Encoder.LiveTranscode;

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;

public class LiveFfmpegRunnerTests
{
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

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        args.Should().Contain("-f").And.Contain("hls");
        args.Should().Contain("-hls_time");
        args[Array.IndexOf(args, "-hls_time") + 1].Should().Be("4");
        args.Should().Contain("-hls_list_size");
        args[Array.IndexOf(args, "-hls_list_size") + 1].Should().Be("0");
        args.Should().Contain("-hls_playlist_type");
        args[Array.IndexOf(args, "-hls_playlist_type") + 1].Should().Be("event");
        args.Should().Contain(arg => arg.EndsWith("index.m3u8"));
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

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        args.Should().NotContain("-ss");
    }

    [Fact]
    public void BuildArguments_IncludesSeek_WhenStartPositionNonZero()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.FromSeconds(120.5),
            Quality: MakeQuality(),
            SegmentDurationSeconds: 6
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        int ssIdx = Array.IndexOf(args, "-ss");
        ssIdx.Should().BeGreaterThanOrEqualTo(0);
        args[ssIdx + 1].Should().Be("120.500");
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

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        int vCodecIdx = Array.IndexOf(args, "-c:v");
        args[vCodecIdx + 1].Should().Be("h264_nvenc");

        int bvIdx = Array.IndexOf(args, "-b:v");
        args[bvIdx + 1].Should().Be("3500k");
    }

    [Fact]
    public void BuildArguments_UsesScaleFilterFromQualityDimensions()
    {
        LiveRunInput input = new(
            InputPath: "/media/in.mkv",
            OutputDirectory: "/tmp/live",
            StartPosition: TimeSpan.Zero,
            Quality: MakeQuality(1280, 720, 4000),
            SegmentDurationSeconds: 4
        );

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        int vfIdx = Array.IndexOf(args, "-vf");
        args[vfIdx + 1].Should().Be("scale=1280:720");
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

        string[] args = LiveFfmpegRunner.BuildArguments(input);

        int progIdx = Array.IndexOf(args, "-progress");
        progIdx.Should().BeGreaterThanOrEqualTo(0);
        args[progIdx + 1].Should().Be("pipe:1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ParsePlaylist
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParsePlaylist_ExtractsDurationAndIndexFromEachEntry()
    {
        string path = WritePlaylist(
            """
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

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = LiveFfmpegRunner.ParsePlaylist(
            path
        );

        entries.Should().HaveCount(3);
        entries[0].Index.Should().Be(0);
        entries[0].Duration.Should().Be(TimeSpan.FromSeconds(6));
        entries[1].Index.Should().Be(1);
        entries[2].Index.Should().Be(2);
        entries[2].Duration.TotalSeconds.Should().BeApproximately(4.04, 0.001);
    }

    [Fact]
    public void ParsePlaylist_IgnoresComments_AndEndlist()
    {
        string path = WritePlaylist(
            """
            #EXTM3U
            #EXT-X-VERSION:3
            #EXT-X-TARGETDURATION:6
            #EXT-X-MEDIA-SEQUENCE:0
            #EXTINF:6.000000,
            seg_00000.ts
            #EXT-X-ENDLIST
            """
        );

        IReadOnlyList<(int Index, TimeSpan Duration)> entries = LiveFfmpegRunner.ParsePlaylist(
            path
        );

        entries.Should().HaveCount(1);
        entries[0].Index.Should().Be(0);
    }

    [Fact]
    public void ParsePlaylist_MissingFile_ReturnsEmpty()
    {
        IReadOnlyList<(int Index, TimeSpan Duration)> entries = LiveFfmpegRunner.ParsePlaylist(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "index.m3u8")
        );

        entries.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RunAsync — integration with fake process runner + playlist file
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PushesSegmentsProducedDuringRun()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"live-runner-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            LiveRunInput input = new(
                InputPath: "/media/in.mkv",
                OutputDirectory: tempDir,
                StartPosition: TimeSpan.Zero,
                Quality: MakeQuality(),
                SegmentDurationSeconds: 6
            );

            FakeProcessRunner runner = new(() =>
            {
                // Simulate FFmpeg producing two segments during its run
                File.WriteAllText(Path.Combine(tempDir, "seg_00000.ts"), new string('a', 100));
                File.WriteAllText(Path.Combine(tempDir, "seg_00001.ts"), new string('a', 100));
                File.WriteAllText(
                    Path.Combine(tempDir, "index.m3u8"),
                    """
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
                runner,
                new EncoderOptions
                {
                    FfmpegPathOverride = "ffmpeg",
                    FfprobePathOverride = "ffprobe",
                },
                NullLogger<LiveFfmpegRunner>.Instance
            );

            LiveSession session = new("sess", MakeQuality());
            session.SetState(LiveSessionState.Transcoding);

            await sut.RunAsync(input, session, CancellationToken.None);

            List<Segment> received = [];
            await foreach (Segment segment in session.Segments)
            {
                received.Add(segment);
                if (received.Count >= 2)
                    break;
            }

            received.Should().HaveCount(2);
            received.Select(s => s.Index).Should().Equal(0, 1);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    private static string WritePlaylist(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"live-pl-{Guid.NewGuid():N}.m3u8");
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
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
                new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
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
                new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero)
            );
        }
    }
}
