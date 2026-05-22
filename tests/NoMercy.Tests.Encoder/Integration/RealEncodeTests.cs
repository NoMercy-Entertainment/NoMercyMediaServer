using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Startup;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using BuiltinPresets = NoMercy.Encoder.Profiles.BuiltinPresets;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LoudnessConfig = NoMercy.Encoder.Profiles.LoudnessConfig;
using LoudnessMode = NoMercy.Encoder.Profiles.LoudnessMode;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitleOutput = NoMercy.Encoder.Profiles.SubtitleOutput;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;
using ThumbnailOutput = NoMercy.Encoder.Profiles.ThumbnailOutput;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Integration;

[Trait("Category", "Integration")]
[Collection("RealEncode")]
public class RealEncodeTests : IAsyncLifetime
{
    private string _testDir = string.Empty;
    private string _inputFile = string.Empty;
    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-encode-test-" + Guid.NewGuid().ToString("N")[..8]
        );
        Directory.CreateDirectory(_testDir);

        _inputFile = Path.Combine(_testDir, "test-input.mp4");

        // Generate a 3-second test clip — short to minimize encode time
        ProcessStartInfo psi = new()
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("testsrc2=size=320x180:rate=25:duration=3");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("lavfi");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("sine=frequency=440:duration=3:sample_rate=44100");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libx264");
        psi.ArgumentList.Add("-preset");
        psi.ArgumentList.Add("ultrafast");
        psi.ArgumentList.Add("-crf");
        psi.ArgumentList.Add("51");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("aac");
        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("64k");
        psi.ArgumentList.Add(_inputFile);

        using Process process = Process.Start(psi)!;
        // Read both streams to prevent buffer deadlock
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg test clip generation failed: {stderr}");
        }

        // Build DI — full encoder pipeline with software-only encoding for deterministic tests
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = "ffmpeg";
            opts.FfprobePathOverride = "ffprobe";
        });

        // Force software encoding — override hardware detector so tests don't depend on GPU
        services.AddSingleton<IHardwareDetector, NullHardwareDetector>();

        _serviceProvider = services.BuildServiceProvider();

        // Probe FFmpeg capabilities
        HardwareInitializationService hwInit =
            _serviceProvider.GetRequiredService<HardwareInitializationService>();
        await hwInit.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _serviceProvider.Dispose();

        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task EncodeAsync_HlsProfile_ProducesPlaylistAndSegments()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        string outputDir = Path.Combine(_testDir, "output");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-180p",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 320,
                Height: 180,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 200,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                // Use Opus — libopus is available in both the bundled FFmpeg and standard builds.
                // libfdk_aac is only in the bundled build (nomercy-ffmpeg), so tests that
                // run with system FFmpeg would fail with "Unknown encoder 'libfdk_aac'".
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
        result.OutputPath.Should().NotBeNullOrWhiteSpace();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Metrics.EncoderUsed.Should().NotBeNullOrWhiteSpace();

        // Verify HLS: at least one playlist and at least one segment
        string[] playlists = Directory.GetFiles(outputDir, "*.m3u8", SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(outputDir, "*.ts", SearchOption.AllDirectories);

        playlists.Should().NotBeEmpty("HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty("HLS output should contain at least one .ts segment");

        // Verify progress observer received at least one callback (stage-completed at end)
        (observer.StagesStarted.Count + observer.ProgressCallCount)
            .Should()
            .BeGreaterThan(0, "should receive at least one progress callback");
    }

    [Fact]
    public async Task EncodeAsync_ScalingProfile_ProducesDownscaledOutput()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        string outputDir = Path.Combine(_testDir, "output-scale");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-90p-scale",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 160,
                Height: null,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 100,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Find the video output directory (template resolves dimensions + colorrange)
        string[] allDirs = Directory.GetDirectories(outputDir);
        string[] allFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
        string[] videoDirs = Directory.GetDirectories(outputDir, "video_*");
        videoDirs
            .Should()
            .HaveCount(
                1,
                $"should have exactly one video output directory. All dirs: [{string.Join(", ", allDirs.Select(Path.GetFileName))}]. All files: [{string.Join(", ", allFiles.Select(f => Path.GetRelativePath(outputDir, f)))}]"
            );

        string[] playlists = Directory.GetFiles(
            videoDirs[0],
            "*.m3u8",
            SearchOption.TopDirectoryOnly
        );
        string[] segments = Directory.GetFiles(videoDirs[0], "*.ts", SearchOption.TopDirectoryOnly);

        playlists.Should().NotBeEmpty("video dir should contain a .m3u8 playlist");
        segments.Should().NotBeEmpty("video dir should contain .ts segments");
    }

    [Fact]
    public async Task EncodeAsync_MultiOutputProfile_ProducesMultipleVariants()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        string outputDir = Path.Combine(_testDir, "output-multi");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-multi",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 320,
                Height: 180,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 200,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: [],
            Ladder: new NoMercy.Encoder.Profiles.LadderConfig
            {
                Mode = NoMercy.Encoder.Profiles.LadderMode.Manual,
                Rungs =
                [
                    new(320, 180, VideoCodecType.H264, 200, 400, 800, 25.0, "ultrafast"),
                    new(160, 90, VideoCodecType.H264, 100, 200, 400, 25.0, "ultrafast"),
                ],
            }
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Should have 2 video variant directories
        string[] videoDirs = Directory.GetDirectories(outputDir, "video_*");
        videoDirs.Should().HaveCount(2, "should have two video output directories");

        foreach (string dir in videoDirs)
        {
            Directory
                .GetFiles(dir, "*.m3u8", SearchOption.TopDirectoryOnly)
                .Should()
                .NotBeEmpty($"{Path.GetFileName(dir)} should contain a .m3u8 playlist");
            Directory
                .GetFiles(dir, "*.ts", SearchOption.TopDirectoryOnly)
                .Should()
                .NotBeEmpty($"{Path.GetFileName(dir)} should contain .ts segments");
        }
    }

    [Fact]
    public async Task EncodeAsync_ScalingProfile_ProducesCorrectResolution()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

        string outputDir = Path.Combine(_testDir, "output-scale-160x90");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-160x90-scaling",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 160,
                Height: 90,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 100,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Verify a video_160x90 (or video_160x90_sdtv) directory exists with playlist + segments
        string[] videoDirs = Directory.GetDirectories(outputDir, "video_160x90*");
        videoDirs
            .Should()
            .HaveCount(
                1,
                $"should have exactly one video output directory for 160x90. Found: [{string.Join(", ", Directory.GetDirectories(outputDir).Select(Path.GetFileName))}]"
            );

        string videoDir = videoDirs[0];
        Directory
            .GetFiles(videoDir, "*.m3u8", SearchOption.TopDirectoryOnly)
            .Should()
            .NotBeEmpty("video_160x90 dir should contain a .m3u8 playlist");
        Directory
            .GetFiles(videoDir, "*.ts", SearchOption.TopDirectoryOnly)
            .Should()
            .NotBeEmpty("video_160x90 dir should contain .ts segments");
    }

    [Fact]
    public async Task EncodeAsync_HlsTwoPassProfile_ProducesValidPlaylist()
    {
        // Two-pass encodes are slower — give them a longer timeout.
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(5));

        string outputDir = Path.Combine(_testDir, "output-twopass");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-twopass-180p",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 320,
                Height: 180,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 500,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: [],
            EncodeMode: EncodeMode.TwoPass
        );

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request, observer, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"Two-pass encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
        result.OutputPath.Should().NotBeNullOrWhiteSpace();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify HLS output: at least one playlist and at least one segment
        string[] playlists = Directory.GetFiles(outputDir, "*.m3u8", SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(outputDir, "*.ts", SearchOption.AllDirectories);

        playlists
            .Should()
            .NotBeEmpty("two-pass HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty("two-pass HLS output should contain at least one .ts segment");

        // Verify all referenced segments actually exist on disk
        foreach (string playlist in playlists)
        {
            string playlistDir = Path.GetDirectoryName(playlist)!;
            string[] lines = await File.ReadAllLinesAsync(playlist, cts.Token);
            IEnumerable<string> segmentRefs = lines.Where(l =>
                !l.StartsWith('#') && (l.EndsWith(".ts") || l.EndsWith(".mp4"))
            );

            foreach (string segRef in segmentRefs)
            {
                string segPath = Path.IsPathRooted(segRef)
                    ? segRef
                    : Path.Combine(playlistDir, segRef);
                File.Exists(segPath)
                    .Should()
                    .BeTrue(
                        $"segment '{segRef}' referenced in '{Path.GetFileName(playlist)}' should exist on disk"
                    );
            }
        }

        // Verify pass-1 stats files are cleaned up after successful two-pass encode
        string statsDir = Path.Combine(outputDir, ".2pass");
        bool anyStatsRemaining =
            Directory.Exists(statsDir)
            && Directory.GetFiles(statsDir, "*.log", SearchOption.AllDirectories).Length > 0;
        anyStatsRemaining
            .Should()
            .BeFalse(
                "pass-1 .log stats files should be deleted after a successful two-pass encode"
            );
    }

    [Fact]
    public void SeedProfiles_DeserializeToValidEncodingProfiles()
    {
        IReadOnlyList<EncodingProfile> profiles = BuiltinPresets.All();

        profiles.Should().NotBeEmpty("BuiltinPresets.All() must return at least one profile");

        foreach (EncodingProfile profile in profiles)
        {
            // Roundtrip: serialize → deserialize
            string json = JsonConvert.SerializeObject(profile);
            EncodingProfile? deserialized = JsonConvert.DeserializeObject<EncodingProfile>(json);

            deserialized
                .Should()
                .NotBeNull($"profile '{profile.Name}' should deserialize without error");
            deserialized!
                .Name.Should()
                .NotBeNullOrWhiteSpace(
                    $"profile '{profile.Name}' should have a non-empty Name after roundtrip"
                );

            bool hasAudioOutput = deserialized.Audio.Length > 0;
            bool hasVideoOutput = deserialized.Video != null || deserialized.Ladder != null;
            (hasAudioOutput || hasVideoOutput)
                .Should()
                .BeTrue($"profile '{profile.Name}' should have at least one audio or video output");
        }
    }

    [Fact]
    public void V3EncodingProfile_SerializationRoundtrip_PreservesAllFields()
    {
        EncodingProfile original = new(
            Id: Ulid.NewUlid(),
            Name: "roundtrip-test-profile",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 23,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Opus,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["eng", "und"],
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    Policy: SubtitlePolicy.Extract,
                    Codec: SubtitleCodecType.WebVtt,
                    AllowedLanguages: ["eng"],
                    IncludeForced: true,
                    OcrLanguage: null,
                    PlaylistNameTemplate: "subs/{lang}"
                ),
            ],
            Thumbnails: new(Width: 320, IntervalSeconds: 10)
        );

        string json = JsonConvert.SerializeObject(original);
        EncodingProfile? deserialized = JsonConvert.DeserializeObject<EncodingProfile>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Container.Should().Be(original.Container);
        deserialized.SchemaVersion.Should().Be(original.SchemaVersion);

        deserialized.Video.Should().NotBeNull();
        deserialized.Video!.Width.Should().Be(1920);
        deserialized.Video.Height.Should().Be(1080);
        deserialized.Video.Codec.Should().Be(VideoCodecType.H264);

        deserialized.Audio.Should().HaveCount(original.Audio.Length);
        deserialized.Audio[0].Codec.Should().Be(AudioCodecType.Opus);
        deserialized.Audio[0].BitrateKbps.Should().Be(128);
        deserialized.Audio[0].AllowedLanguages.Should().BeEquivalentTo("eng", "und");

        deserialized.Subtitles.Should().HaveCount(original.Subtitles.Length);
        deserialized.Subtitles[0].Codec.Should().Be(SubtitleCodecType.WebVtt);
        deserialized.Subtitles[0].Policy.Should().Be(SubtitlePolicy.Extract);

        deserialized.Thumbnails.Should().NotBeNull();
        deserialized.Thumbnails!.Width.Should().Be(320);
        deserialized.Thumbnails.IntervalSeconds.Should().Be(10);
    }

    private class TestProgressObserver : IProgressObserver
    {
        public List<string> StagesStarted { get; } = [];
        public int ProgressCallCount { get; private set; }

        public void OnStageStarted(string stageName) => StagesStarted.Add(stageName);

        public void OnProgress(EncodingProgress progress) => ProgressCallCount++;

        public void OnStageCompleted(string stageName, TimeSpan duration) { }

        public void OnCompleted() { }

        public void OnPlanResolved(
            List<string> videoStreams,
            List<string> audioStreams,
            List<string> subtitleStreams,
            bool hasGpu,
            bool isHdr
        ) { }

        public void OnError(EncodingError error) { }
    }
}
