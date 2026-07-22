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

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Startup;
using BuiltinPresets = NoMercy.Encoder.Profiles.BuiltinPresets;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using HardwarePreference = NoMercy.Encoder.Profiles.HardwarePreference;
using HlsDerivatives = NoMercy.Encoder.Profiles.HlsDerivatives;
using LoudnessConfig = NoMercy.Encoder.Profiles.LoudnessConfig;
using LoudnessMode = NoMercy.Encoder.Profiles.LoudnessMode;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;

namespace NoMercy.Tests.Encoder.Integration;

[Trait(name: "Category", value: "Integration")]
[Collection(name: "RealEncode")]
public class RealEncodeTests : IAsyncLifetime
{
    private const string ForkRequiredSkipReason =
        "Requires nomercy-ffmpeg (spritevtt muxer). Set NOMERCY_FFMPEG_PATH or install via "
        + "AppData/Local/NoMercy_dev/binaries/ffmpeg.";

    private string _testDir = string.Empty;
    private string _inputFile = string.Empty;
    private ServiceProvider _serviceProvider = null!;
    private string? _ffmpegPath;
    private string? _ffprobePath;
    private bool _forkSupportsSpritevtt;

    public async Task InitializeAsync()
    {
        // Resolve nomercy-ffmpeg before anything else. Real-encode tests use
        // the spritevtt muxer (fork-only) — pointing FfmpegPathOverride at the
        // system "ffmpeg" PATH would surface as "Unrecognized option
        // 'vtt_filename'" and report environment problems as code failures.
        _ffmpegPath = NoMercyFfmpegProbe.ResolveFfmpegPath();
        _ffprobePath = NoMercyFfmpegProbe.ResolveFfprobePath(ffmpegPath: _ffmpegPath);
        _forkSupportsSpritevtt =
            _ffmpegPath is not null && NoMercyFfmpegProbe.SupportsSpritevtt(ffmpegPath: _ffmpegPath);

        _testDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-encode-test-" + Guid.NewGuid().ToString(format: "N")[..8]
        );
        Directory.CreateDirectory(path: _testDir);

        _inputFile = Path.Combine(path1: _testDir, path2: "test-input.mp4");

        // Generate a 3-second test clip — short to minimize encode time. Use
        // the resolved fork binary so the clip + the encode under test agree
        // on which ffmpeg they're driving.
        ProcessStartInfo psi = new()
        {
            FileName = _ffmpegPath ?? "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(item: "-y");
        psi.ArgumentList.Add(item: "-f");
        psi.ArgumentList.Add(item: "lavfi");
        psi.ArgumentList.Add(item: "-i");
        psi.ArgumentList.Add(item: "testsrc2=size=320x180:rate=25:duration=3");
        psi.ArgumentList.Add(item: "-f");
        psi.ArgumentList.Add(item: "lavfi");
        psi.ArgumentList.Add(item: "-i");
        psi.ArgumentList.Add(item: "sine=frequency=440:duration=3:sample_rate=44100");
        psi.ArgumentList.Add(item: "-c:v");
        psi.ArgumentList.Add(item: "libx264");
        psi.ArgumentList.Add(item: "-preset");
        psi.ArgumentList.Add(item: "ultrafast");
        psi.ArgumentList.Add(item: "-crf");
        psi.ArgumentList.Add(item: "51");
        psi.ArgumentList.Add(item: "-c:a");
        psi.ArgumentList.Add(item: "aac");
        psi.ArgumentList.Add(item: "-b:a");
        psi.ArgumentList.Add(item: "64k");
        psi.ArgumentList.Add(item: _inputFile);

        using Process process = Process.Start(startInfo: psi)!;
        // Read both streams to prevent buffer deadlock
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        string stderr = await stderrTask;
        await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(message: $"FFmpeg test clip generation failed: {stderr}");
        }

        // Build DI — full encoder pipeline with software-only encoding for deterministic tests
        ServiceCollection services = new();
        services.AddLogging();
        // EF Core db-context factories needed by DbDriverFingerprintStore /
        // subscribers — in-memory provider keeps integration tests hermetic.
        string suffix = Guid.NewGuid().ToString(format: "N")[..8];
        services.AddDbContextFactory<MediaContext>(optionsAction: o =>
            o.UseInMemoryDatabase(databaseName: $"real-media-{suffix}")
        );
        services.AddDbContextFactory<AppDbContext>(optionsAction: o =>
            o.UseInMemoryDatabase(databaseName: $"real-app-{suffix}")
        );
        services.AddNoMercyEncoder(configure: opts =>
        {
            opts.FfmpegPathOverride = _ffmpegPath ?? "ffmpeg";
            opts.FfprobePathOverride = _ffprobePath ?? "ffprobe";
        });

        // Force software encoding — override hardware detector so tests don't depend on GPU
        services.AddSingleton<IHardwareDetector, NullHardwareDetector>();

        // IHostApplicationLifetime is normally added by HostBuilder; stub it
        // here so resolving HardwareInitializationService (which depends on
        // BenchmarkJobTracker) works without a full host.
        services.AddSingleton<IHostApplicationLifetime, TestHostLifetime>();

        _serviceProvider = services.BuildServiceProvider();

        // Probe FFmpeg capabilities
        HardwareInitializationService hwInit =
            _serviceProvider.GetRequiredService<HardwareInitializationService>();
        await hwInit.StartAsync(cancellationToken: CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        _serviceProvider.Dispose();

        try
        {
            if (Directory.Exists(path: _testDir))
                Directory.Delete(path: _testDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }

        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task EncodeAsync_HlsProfile_ProducesPlaylistAndSegments()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 3));

        string outputDir = Path.Combine(path1: _testDir, path2: "output");
        Directory.CreateDirectory(path: outputDir);

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
                // Use MP3 — libmp3lame is in standard FFmpeg builds AND is a valid HlsTs
                // codec. libfdk_aac (AudioCodecType.Aac) is bundle-only and Opus is
                // not in the HlsTs compatibility matrix, so neither works for these
                // integration tests when running against system FFmpeg.
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
        result.OutputPath.Should().NotBeNullOrWhiteSpace();
        result.Duration.Should().BeGreaterThan(expected: TimeSpan.Zero);
        result.Metrics.Should().NotBeNull();
        result.Metrics!.EncoderUsed.Should().NotBeNullOrWhiteSpace();

        // Verify HLS: at least one playlist and at least one segment
        string[] playlists = Directory.GetFiles(path: outputDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        playlists.Should().NotBeEmpty(because: "HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty(because: "HLS output should contain at least one .ts segment");

        // Verify progress observer received at least one callback (stage-completed at end)
        (observer.StagesStarted.Count + observer.ProgressCallCount)
            .Should()
            .BeGreaterThan(expected: 0, because: "should receive at least one progress callback");
    }

    [SkippableFact]
    public async Task EncodeAsync_ScalingProfile_ProducesDownscaledOutput()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 3));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-scale");
        Directory.CreateDirectory(path: outputDir);

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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Find the video output directory (template resolves dimensions + colorrange)
        string[] allDirs = Directory.GetDirectories(path: outputDir);
        string[] allFiles = Directory.GetFiles(path: outputDir, searchPattern: "*", searchOption: SearchOption.AllDirectories);
        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_*");
        videoDirs
            .Should()
            .HaveCount(
                expected: 1,
                because: $"should have exactly one video output directory. All dirs: [{string.Join(separator: ", ", values: allDirs.Select(selector: Path.GetFileName))}]. All files: [{string.Join(separator: ", ", values: allFiles.Select(selector: f => Path.GetRelativePath(relativeTo: outputDir, path: f)))}]"
            );

        string[] playlists = Directory.GetFiles(
            path: videoDirs[0],
            searchPattern: "*.m3u8",
            searchOption: SearchOption.TopDirectoryOnly
        );
        string[] segments = Directory.GetFiles(path: videoDirs[0], searchPattern: "*.ts", searchOption: SearchOption.TopDirectoryOnly);

        playlists.Should().NotBeEmpty(because: "video dir should contain a .m3u8 playlist");
        segments.Should().NotBeEmpty(because: "video dir should contain .ts segments");
    }

    [SkippableFact]
    public async Task EncodeAsync_MultiOutputProfile_ProducesMultipleVariants()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 3));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-multi");
        Directory.CreateDirectory(path: outputDir);

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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: [],
            Ladder: new()
            {
                Mode = NoMercy.Encoder.Profiles.LadderMode.Manual,
                // Validator requires manual rungs sorted ascending by bitrate
                // (100 kbps before 200 kbps) — keeps master-playlist BANDWIDTH
                // ordering deterministic.
                Rungs =
                [
                    new(Width: 160, Height: 90, Codec: VideoCodecType.H264, BitrateKbps: 100, MaxBitrateKbps: 200, BufferSizeKbps: 400, Framerate: 25.0, Preset: "ultrafast"),
                    new(Width: 320, Height: 180, Codec: VideoCodecType.H264, BitrateKbps: 200, MaxBitrateKbps: 400, BufferSizeKbps: 800, Framerate: 25.0, Preset: "ultrafast"),
                ],
            }
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Should have 2 video variant directories
        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_*");
        videoDirs.Should().HaveCount(expected: 2, because: "should have two video output directories");

        foreach (string dir in videoDirs)
        {
            Directory
                .GetFiles(path: dir, searchPattern: "*.m3u8", searchOption: SearchOption.TopDirectoryOnly)
                .Should()
                .NotBeEmpty(because: $"{Path.GetFileName(path: dir)} should contain a .m3u8 playlist");
            Directory
                .GetFiles(path: dir, searchPattern: "*.ts", searchOption: SearchOption.TopDirectoryOnly)
                .Should()
                .NotBeEmpty(because: $"{Path.GetFileName(path: dir)} should contain .ts segments");
        }
    }

    [SkippableFact]
    public async Task EncodeAsync_ScalingProfile_ProducesCorrectResolution()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 3));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-scale-160x90");
        Directory.CreateDirectory(path: outputDir);

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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Verify a video_160x90 (or video_160x90_sdtv) directory exists with playlist + segments
        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_160x90*");
        videoDirs
            .Should()
            .HaveCount(
                expected: 1,
                because: $"should have exactly one video output directory for 160x90. Found: [{string.Join(separator: ", ", values: Directory.GetDirectories(path: outputDir).Select(selector: Path.GetFileName))}]"
            );

        string videoDir = videoDirs[0];
        Directory
            .GetFiles(path: videoDir, searchPattern: "*.m3u8", searchOption: SearchOption.TopDirectoryOnly)
            .Should()
            .NotBeEmpty(because: "video_160x90 dir should contain a .m3u8 playlist");
        Directory
            .GetFiles(path: videoDir, searchPattern: "*.ts", searchOption: SearchOption.TopDirectoryOnly)
            .Should()
            .NotBeEmpty(because: "video_160x90 dir should contain .ts segments");
    }

    [SkippableFact]
    public async Task EncodeAsync_HlsTwoPassProfile_ProducesValidPlaylist()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);
        // Two-pass encodes are slower — give them a longer timeout.
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 5));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-twopass");
        Directory.CreateDirectory(path: outputDir);

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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 64,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: [],
            EncodeMode: EncodeMode.TwoPass
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Two-pass encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
        result.OutputPath.Should().NotBeNullOrWhiteSpace();
        result.Duration.Should().BeGreaterThan(expected: TimeSpan.Zero);

        // Verify HLS output: at least one playlist and at least one segment
        string[] playlists = Directory.GetFiles(path: outputDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        playlists
            .Should()
            .NotBeEmpty(because: "two-pass HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty(because: "two-pass HLS output should contain at least one .ts segment");

        // Verify all referenced segments actually exist on disk
        foreach (string playlist in playlists)
        {
            string playlistDir = Path.GetDirectoryName(path: playlist)!;
            string[] lines = await File.ReadAllLinesAsync(path: playlist, cancellationToken: cts.Token);
            IEnumerable<string> segmentRefs = lines.Where(predicate: l =>
                !l.StartsWith(value: '#') && (l.EndsWith(value: ".ts") || l.EndsWith(value: ".mp4"))
            );

            foreach (string segRef in segmentRefs)
            {
                string segPath = Path.IsPathRooted(path: segRef)
                    ? segRef
                    : Path.Combine(path1: playlistDir, path2: segRef);
                File.Exists(path: segPath)
                    .Should()
                    .BeTrue(
                        because: $"segment '{segRef}' referenced in '{Path.GetFileName(path: playlist)}' should exist on disk"
                    );
            }
        }

        // Verify pass-1 stats files are cleaned up after successful two-pass encode
        string statsDir = Path.Combine(path1: outputDir, path2: ".2pass");
        bool anyStatsRemaining =
            Directory.Exists(path: statsDir)
            && Directory.GetFiles(path: statsDir, searchPattern: "*.log", searchOption: SearchOption.AllDirectories).Length > 0;
        anyStatsRemaining
            .Should()
            .BeFalse(
                because: "pass-1 .log stats files should be deleted after a successful two-pass encode"
            );
    }

    [Fact]
    public void SeedProfiles_DeserializeToValidEncodingProfiles()
    {
        IReadOnlyList<EncodingProfile> profiles = BuiltinPresets.All();

        profiles.Should().NotBeEmpty(because: "BuiltinPresets.All() must return at least one profile");

        foreach (EncodingProfile profile in profiles)
        {
            // Roundtrip: serialize → deserialize
            string json = JsonConvert.SerializeObject(value: profile);
            EncodingProfile? deserialized = JsonConvert.DeserializeObject<EncodingProfile>(value: json);

            deserialized
                .Should()
                .NotBeNull(because: $"profile '{profile.Name}' should deserialize without error");
            deserialized!
                .Name.Should()
                .NotBeNullOrWhiteSpace(
                    because: $"profile '{profile.Name}' should have a non-empty Name after roundtrip"
                );

            bool hasAudioOutput = deserialized.Audio.Length > 0;
            bool hasVideoOutput = deserialized.Video != null || deserialized.Ladder != null;
            (hasAudioOutput || hasVideoOutput)
                .Should()
                .BeTrue(because: $"profile '{profile.Name}' should have at least one audio or video output");
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
                    Codec: AudioCodecType.Mp3,
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

        string json = JsonConvert.SerializeObject(value: original);
        EncodingProfile? deserialized = JsonConvert.DeserializeObject<EncodingProfile>(value: json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(expected: original.Name);
        deserialized.Container.Should().Be(expected: original.Container);
        deserialized.SchemaVersion.Should().Be(expected: original.SchemaVersion);

        deserialized.Video.Should().NotBeNull();
        deserialized.Video!.Width.Should().Be(expected: 1920);
        deserialized.Video.Height.Should().Be(expected: 1080);
        deserialized.Video.Codec.Should().Be(expected: VideoCodecType.H264);

        deserialized.Audio.Should().HaveCount(expected: original.Audio.Length);
        deserialized.Audio[0].Codec.Should().Be(expected: AudioCodecType.Mp3);
        deserialized.Audio[0].BitrateKbps.Should().Be(expected: 128);
        deserialized.Audio[0].AllowedLanguages.Should().BeEquivalentTo(expectation: ["eng", "und"]);

        deserialized.Subtitles.Should().HaveCount(expected: original.Subtitles.Length);
        deserialized.Subtitles[0].Codec.Should().Be(expected: SubtitleCodecType.WebVtt);
        deserialized.Subtitles[0].Policy.Should().Be(expected: SubtitlePolicy.Extract);

        deserialized.Thumbnails.Should().NotBeNull();
        deserialized.Thumbnails!.Width.Should().Be(expected: 320);
        deserialized.Thumbnails.IntervalSeconds.Should().Be(expected: 10);
    }

    // -------------------------------------------------------------------------
    // Regression matrix: four content classes from the encoder plan.
    // Each resolves a fixture file; if absent, the test skips cleanly.
    // Fixture root: NOMERCY_TEST_FIXTURES_PATH env var, or
    //   %LOCALAPPDATA%/NoMercy_dev/test-fixtures (Windows) /
    //   ~/.local/share/NoMercy_dev/test-fixtures (Linux).
    // -------------------------------------------------------------------------

    // Resolves a fixture by name. Prefers a real media file on disk
    // (NOMERCY_TEST_FIXTURES_PATH or the NoMercy_dev/test-fixtures dirs); when
    // none is present, synthesizes a deterministic stand-in with the resolved
    // fork ffmpeg so the regression test runs everywhere instead of skipping.
    // Generic file names — these are lavfi-generated, not real titles.
    private string ResolveOrGenerateFixture(string relativeFileName)
    {
        string? real = ResolveRealFixture(relativeFileName: relativeFileName);
        if (real is not null)
            return real;

        return GenerateSyntheticFixture(relativeFileName: relativeFileName);
    }

    private static string? ResolveRealFixture(string relativeFileName)
    {
        string? envRoot = Environment.GetEnvironmentVariable(variable: "NOMERCY_TEST_FIXTURES_PATH");
        if (!string.IsNullOrWhiteSpace(value: envRoot))
        {
            string candidate = Path.Combine(path1: envRoot, path2: relativeFileName);
            if (File.Exists(path: candidate))
                return candidate;
        }

        string? localAppData = Environment.GetFolderPath(
            folder: Environment.SpecialFolder.LocalApplicationData
        );
        string? home = Environment.GetEnvironmentVariable(variable: "HOME");

        foreach (
            string root in new[]
            {
                !string.IsNullOrWhiteSpace(value: localAppData)
                    ? Path.Combine(path1: localAppData, path2: "NoMercy_dev", path3: "test-fixtures")
                    : null,
                !string.IsNullOrWhiteSpace(value: localAppData)
                    ? Path.Combine(path1: localAppData, path2: "NoMercy", path3: "test-fixtures")
                    : null,
                !string.IsNullOrWhiteSpace(value: home)
                    ? Path.Combine(paths: [home, ".local", "share", "NoMercy_dev", "test-fixtures"])
                    : null,
            }.OfType<string>()
        )
        {
            string candidate = Path.Combine(path1: root, path2: relativeFileName);
            if (File.Exists(path: candidate))
                return candidate;
        }

        return null;
    }

    // lavfi recipes per fixture name. Each is a short, deterministic clip that
    // exercises the content class the test cares about (SD, 1080p, HDR10 PQ,
    // audio-only) without depending on real media.
    private string GenerateSyntheticFixture(string relativeFileName)
    {
        string outPath = Path.Combine(path1: _testDir, path2: relativeFileName);
        if (File.Exists(path: outPath))
            return outPath;

        List<string> args = relativeFileName switch
        {
            "video-sd480p.mkv" =>
            [
                "-f",
                "lavfi",
                "-i",
                "testsrc2=size=854x480:rate=25:duration=5",
                "-f",
                "lavfi",
                "-i",
                "sine=frequency=440:duration=5",
                "-c:v",
                "libx264",
                "-preset",
                "ultrafast",
                "-crf",
                "30",
                "-c:a",
                "aac",
                "-b:a",
                "96k",
            ],
            "video-1080p.mkv" =>
            [
                "-f",
                "lavfi",
                "-i",
                "testsrc2=size=1920x1080:rate=25:duration=5",
                "-f",
                "lavfi",
                "-i",
                "sine=frequency=440:duration=5",
                "-c:v",
                "libx264",
                "-preset",
                "ultrafast",
                "-crf",
                "30",
                "-c:a",
                "aac",
                "-b:a",
                "96k",
            ],
            "video-hdr10.mkv" =>
            [
                "-f",
                "lavfi",
                "-i",
                "testsrc2=size=1920x1080:rate=25:duration=5",
                "-vf",
                "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc",
                "-c:v",
                "libx265",
                "-preset",
                "ultrafast",
                "-x265-params",
                "hdr10=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc",
                "-color_primaries",
                "bt2020",
                "-color_trc",
                "smpte2084",
                "-colorspace",
                "bt2020nc",
            ],
            "audio-only.flac" =>
            [
                "-f",
                "lavfi",
                "-i",
                "sine=frequency=440:duration=5",
                "-c:a",
                "flac",
            ],
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(relativeFileName),
                message: $"No synthetic recipe for fixture '{relativeFileName}'"
            ),
        };

        ProcessStartInfo psi = new()
        {
            FileName = _ffmpegPath ?? "ffmpeg",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(item: "-y");
        foreach (string arg in args)
            psi.ArgumentList.Add(item: arg);
        psi.ArgumentList.Add(item: outPath);

        using Process process = Process.Start(startInfo: psi)!;
        string stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                message: $"Synthetic fixture generation failed for '{relativeFileName}': {stderr}"
            );

        return outPath;
    }

    [SkippableFact]
    public async Task EncodeAsync_Sd480p_DarkwingDuck_ProducesPlaylistAndSegments()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);

        string fixture = ResolveOrGenerateFixture(relativeFileName: "video-sd480p.mkv");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 10));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-sd480p");
        Directory.CreateDirectory(path: outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-sd480p",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 854,
                Height: 480,
                RateControl: RateControlMode.Crf,
                Crf: 28,
                BitrateKbps: 800,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.Baseline,
                Level: "3.1",
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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und", "eng"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: fixture,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"SD 480p encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        string[] playlists = Directory.GetFiles(path: outputDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        playlists
            .Should()
            .NotBeEmpty(because: "SD 480p HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty(because: "SD 480p HLS output should contain at least one .ts segment");

        // Verify the output directory contains a video folder for the expected resolution
        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_854x480*");
        videoDirs
            .Should()
            .HaveCount(
                expected: 1,
                because: $"should have exactly one video_854x480 directory. Found: [{string.Join(separator: ", ", values: Directory.GetDirectories(path: outputDir).Select(selector: Path.GetFileName))}]"
            );
    }

    [SkippableFact]
    public async Task EncodeAsync_1080p_Anime_NoGameNoLife_ProducesPlaylistAndSegments()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);

        string fixture = ResolveOrGenerateFixture(relativeFileName: "video-1080p.mkv");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 15));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-1080p-anime");
        Directory.CreateDirectory(path: outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-1080p-anime",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 20,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: "animation",
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
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["jpn", "und"],
                    DefaultLanguage: "jpn",
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: fixture,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"1080p anime encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        string[] playlists = Directory.GetFiles(path: outputDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        playlists
            .Should()
            .NotBeEmpty(because: "1080p anime HLS output should contain at least one .m3u8 playlist");
        segments
            .Should()
            .NotBeEmpty(because: "1080p anime HLS output should contain at least one .ts segment");

        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_1920x1080*");
        videoDirs
            .Should()
            .HaveCount(
                expected: 1,
                because: $"should have exactly one video_1920x1080 directory. Found: [{string.Join(separator: ", ", values: Directory.GetDirectories(path: outputDir).Select(selector: Path.GetFileName))}]"
            );
    }

    [SkippableFact]
    public async Task EncodeAsync_HdrContent_ProducesTonemappedSdrPlaylist()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);

        string fixture = ResolveOrGenerateFixture(relativeFileName: "video-hdr10.mkv");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 15));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-hdr");
        Directory.CreateDirectory(path: outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-hdr-tonemap",
            Container: Container.HlsTs,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: RateControlMode.Crf,
                Crf: 22,
                BitrateKbps: 4000,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: "yuv420p",
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: true,
                SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
                PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und", "eng"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: fixture,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"HDR tonemapping encode failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        string[] playlists = Directory.GetFiles(path: outputDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories);
        string[] segments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        playlists.Should().NotBeEmpty(because: "HDR HLS output should contain at least one .m3u8 playlist");
        segments.Should().NotBeEmpty(because: "HDR HLS output should contain at least one .ts segment");

        // Master playlist must declare SDR range (tonemapping was applied)
        string? masterPlaylist = playlists.FirstOrDefault(predicate: p =>
            Path.GetFileName(path: p).Equals(value: "master.m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
        );
        if (masterPlaylist is not null)
        {
            string masterContent = await File.ReadAllTextAsync(path: masterPlaylist, cancellationToken: cts.Token);
            masterContent
                .Should()
                .NotContain(
                    unexpected: "VIDEO-RANGE=PQ",
                    because: "tonemapping to SDR should remove HDR PQ video range from master playlist"
                );
        }
    }

    [SkippableFact]
    public async Task EncodeAsync_AudioOnly_ProducesAudioHlsPlaylist()
    {
        Skip.IfNot(condition: _forkSupportsSpritevtt, reason: ForkRequiredSkipReason);

        string fixture = ResolveOrGenerateFixture(relativeFileName: "audio-only.flac");

        using CancellationTokenSource cts = new(delay: TimeSpan.FromMinutes(minutes: 5));

        string outputDir = Path.Combine(path1: _testDir, path2: "output-audio-only");
        Directory.CreateDirectory(path: outputDir);

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "test-hls-audio-only",
            Container: Container.HlsTs,
            Video: null,
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 320,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und", "eng"],
                    DefaultLanguage: null,
                    Loudness: new(Mode: LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
                    PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

        EncodingRequest request = new(
            InputPath: fixture,
            OutputDirectory: outputDir,
            Profile: profile
        );

        TestProgressObserver observer = new();
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();

        EncodingResult result = await encoder.EncodeAsync(request: request, progress: observer, ct: cts.Token);

        result
            .Success.Should()
            .BeTrue(
                because: $"Audio-only encoding failed: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );

        // Audio-only: must produce audio segment directories and playlists; must NOT produce video dirs
        string[] allPlaylists = Directory.GetFiles(
            path: outputDir,
            searchPattern: "*.m3u8",
            searchOption: SearchOption.AllDirectories
        );
        string[] audioSegments = Directory.GetFiles(path: outputDir, searchPattern: "*.ts", searchOption: SearchOption.AllDirectories);

        allPlaylists
            .Should()
            .NotBeEmpty(because: "audio-only output should contain at least one .m3u8 playlist");
        audioSegments
            .Should()
            .NotBeEmpty(because: "audio-only output should contain at least one .ts segment");

        string[] videoDirs = Directory.GetDirectories(path: outputDir, searchPattern: "video_*");
        videoDirs.Should().BeEmpty(because: "audio-only encode should not produce any video_ directories");

        string[] audioDirs = Directory.GetDirectories(path: outputDir, searchPattern: "audio_*");
        audioDirs
            .Should()
            .NotBeEmpty(because: "audio-only encode should produce at least one audio_ directory");
    }

    private sealed class TestHostLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopping { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopped { get; } = CancellationToken.None;

        public void StopApplication() { }
    }

    private class TestProgressObserver : IProgressObserver
    {
        public List<string> StagesStarted { get; } = [];
        public int ProgressCallCount { get; private set; }

        public void OnStageStarted(string stageName) => StagesStarted.Add(item: stageName);

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
