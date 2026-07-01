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
using NoMercy.Database;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Startup;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using ContainerCompatibility = NoMercy.Encoder.Profiles.ContainerCompatibility;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using HardwarePreference = NoMercy.Encoder.Profiles.HardwarePreference;
using HlsDerivatives = NoMercy.Encoder.Profiles.HlsDerivatives;
using LoudnessConfig = NoMercy.Encoder.Profiles.LoudnessConfig;
using LoudnessMode = NoMercy.Encoder.Profiles.LoudnessMode;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Integration;

/// <summary>
/// The execution oracle: ffmpeg validates its OWN thousands of codec / container
/// / muxer rules. For every valid (container, video codec, audio codec) triple in
/// <see cref="ContainerCompatibility"/> that THIS ffmpeg can actually encode, the
/// pipeline builds the real command and runs a 1-second encode — and ffmpeg
/// accepting the command is the proof the args are valid. A static assertion can
/// only check the rules we thought to write; running ffmpeg catches the rest.
///
/// Codecs whose encoder is absent from this build (fork-only libfdk_aac,
/// experimental truehd/dca, etc.) are SKIPPED, not failed — an environment gap
/// is not a code bug. When a case fails, the ffmpeg stderr is surfaced so a real
/// codec/container rule violation is visible.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RealEncode")]
public class FfmpegMatrixOracleTests : IAsyncLifetime
{
    private string _testDir = string.Empty;
    private string _inputFile = string.Empty;
    private ServiceProvider _serviceProvider = null!;
    private string? _ffmpegPath;
    private string? _ffprobePath;
    private IReadOnlySet<string> _availableEncoders = new HashSet<string>();

    public async Task InitializeAsync()
    {
        // Fork first (custom muxers), then env, then stock ffmpeg on PATH — the
        // oracle only needs standard codecs, so a stock build is enough.
        _ffmpegPath = NoMercyFfmpegProbe.ResolveFfmpegPath() ?? "ffmpeg";
        _ffprobePath = NoMercyFfmpegProbe.ResolveFfprobePath(_ffmpegPath) ?? "ffprobe";

        _testDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy-oracle-" + Guid.NewGuid().ToString("N")[..8]
        );
        Directory.CreateDirectory(_testDir);
        _inputFile = Path.Combine(_testDir, "src.mp4");

        // A 1-second 10-bit-capable synthetic clip. yuv420p10le lets the 10-bit
        // profiles encode without a source-depth mismatch.
        int exit = await RunFfmpegAsync([
            "-y",
            "-f",
            "lavfi",
            "-i",
            "testsrc2=size=320x180:rate=25:duration=1",
            "-f",
            "lavfi",
            "-i",
            "sine=frequency=440:duration=1:sample_rate=48000",
            "-c:v",
            "libx264",
            "-preset",
            "ultrafast",
            "-pix_fmt",
            "yuv420p",
            "-c:a",
            "aac",
            _inputFile,
        ]);
        if (exit != 0)
            throw new InvalidOperationException("Oracle test-clip generation failed.");

        _availableEncoders = await ProbeAvailableEncodersAsync();

        ServiceCollection services = new();
        services.AddLogging();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        services.AddDbContextFactory<MediaContext>(o =>
            o.UseInMemoryDatabase($"oracle-media-{suffix}")
        );
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseInMemoryDatabase($"oracle-app-{suffix}")
        );
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = _ffmpegPath;
            opts.FfprobePathOverride = _ffprobePath;
        });
        services.AddSingleton<IHardwareDetector, NullHardwareDetector>();
        _serviceProvider = services.BuildServiceProvider();

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
            // best-effort
        }
        return Task.CompletedTask;
    }

    // Containers whose file output the oracle drives through a single-file
    // encode. Segmented HLS/DASH are exercised by the dedicated RealEncodeTests;
    // here we cover the codec/container muxer-rule surface with muxed files.
    public static IEnumerable<object[]> Matrix()
    {
        (Container container, VideoCodecType[] videos, AudioCodecType[] audios)[] cases =
        [
            (
                Container.Mp4,
                [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
                [AudioCodecType.Aac, AudioCodecType.Ac3, AudioCodecType.Eac3, AudioCodecType.Mp3]
            ),
            (
                Container.Mkv,
                [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9],
                [
                    AudioCodecType.Aac,
                    AudioCodecType.Mp3,
                    AudioCodecType.Opus,
                    AudioCodecType.Flac,
                    AudioCodecType.Ac3,
                    AudioCodecType.Eac3,
                    AudioCodecType.Vorbis,
                ]
            ),
        ];

        foreach ((Container container, VideoCodecType[] videos, AudioCodecType[] audios) in cases)
        foreach (VideoCodecType video in videos)
        foreach (AudioCodecType audio in audios)
        {
            // Only emit pairs the support matrix actually allows.
            if (
                !ContainerCompatibility.SupportsVideo(container, video)
                || !ContainerCompatibility.SupportsAudio(container, audio)
            )
                continue;

            yield return [container, video, audio];
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Matrix))]
    public async Task ValidCodecContainerPair_FfmpegAcceptsTheGeneratedCommand(
        Container container,
        VideoCodecType video,
        AudioCodecType audio
    )
    {
        string videoEncoder = VideoEncoderName(video);
        string audioEncoder = AudioCodecDefinitions.GetEncoder(audio).FfmpegName;

        // Skip codecs THIS ffmpeg build cannot encode — an environment gap, not a
        // code defect. fdk_aac is fork-only; some HW/experimental encoders absent.
        Skip.IfNot(
            _availableEncoders.Contains(videoEncoder),
            $"video encoder {videoEncoder} not available in this ffmpeg build"
        );
        Skip.IfNot(
            _availableEncoders.Contains(audioEncoder),
            $"audio encoder {audioEncoder} not available in this ffmpeg build"
        );

        string outputDir = Path.Combine(_testDir, $"{container}_{video}_{audio}");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = BuildProfile(container, video, audio);
        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();
        EncodingResult result = await encoder.EncodeAsync(request, progress: null, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"ffmpeg must accept {container}/{video}/{audio}. "
                    + $"Error: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
    }

    // Bit-depth × explicit-codec-profile axis. The bug this catches only appears
    // when a 10-bit output carries an 8-bit-only H.26x profile string, and only
    // when the profile is EXPLICIT (Auto masks it on some encoders). Every 10-bit
    // software codec is exercised at 8-bit AND 10-bit, each against every
    // CodecProfile tier the user can set. ffmpeg accepting the command is proof
    // the pipeline couples the profile string to the pixel format.
    public static IEnumerable<object[]> BitDepthProfileMatrix()
    {
        (VideoCodecType video, CodecProfile[] profiles)[] videoCases =
        [
            (
                VideoCodecType.H264,
                [CodecProfile.Auto, CodecProfile.Main, CodecProfile.High, CodecProfile.High10]
            ),
            (VideoCodecType.H265, [CodecProfile.Auto, CodecProfile.Main, CodecProfile.Main10]),
        ];

        foreach ((VideoCodecType video, CodecProfile[] profiles) in videoCases)
        foreach (int bitDepth in new[] { 8, 10 })
        foreach (CodecProfile codecProfile in profiles)
            yield return [video, bitDepth, codecProfile];
    }

    [SkippableTheory]
    [MemberData(nameof(BitDepthProfileMatrix))]
    public async Task BitDepthAndProfile_FfmpegAcceptsTheGeneratedCommand(
        VideoCodecType video,
        int bitDepth,
        CodecProfile codecProfile
    )
    {
        string videoEncoder = VideoEncoderName(video);
        Skip.IfNot(
            _availableEncoders.Contains(videoEncoder),
            $"video encoder {videoEncoder} not available in this ffmpeg build"
        );

        string outputDir = Path.Combine(_testDir, $"bd_{video}_{bitDepth}_{codecProfile}");
        Directory.CreateDirectory(outputDir);

        EncodingProfile profile = BuildProfile(
            Container.Mkv,
            video,
            AudioCodecType.Aac,
            bitDepth,
            codecProfile
        );
        EncodingRequest request = new(
            InputPath: _inputFile,
            OutputDirectory: outputDir,
            Profile: profile
        );

        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));
        IEncoder encoder = _serviceProvider.GetRequiredService<IEncoder>();
        EncodingResult result = await encoder.EncodeAsync(request, progress: null, cts.Token);

        result
            .Success.Should()
            .BeTrue(
                $"ffmpeg must accept {video} {bitDepth}-bit profile={codecProfile}. "
                    + $"Error: {result.Error?.Message} | stderr: {result.Error?.FfmpegStderr}"
            );
    }

    private static EncodingProfile BuildProfile(
        Container container,
        VideoCodecType video,
        AudioCodecType audio,
        int bitDepth = 8,
        CodecProfile codecProfile = CodecProfile.Auto
    ) =>
        new(
            Id: Ulid.NewUlid(),
            Name: $"oracle-{container}-{video}-{audio}",
            Container: container,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: video,
                Width: 320,
                Height: 180,
                RateControl: RateControlMode.Crf,
                Crf: 40,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "ultrafast",
                CodecProfile: codecProfile,
                Level: null,
                Tune: null,
                BitDepth: bitDepth,
                PixelFormat: null,
                KeyframeIntervalSeconds: 2,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/:framesize:",
                PlaylistNameTemplate: "video/:framesize:/playlist"
            ),
            Audio:
            [
                new AudioOutput(
                    Policy: StreamPolicy.Transcode,
                    Codec: audio,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: ["und"],
                    DefaultLanguage: null,
                    Loudness: new LoudnessConfig(LoudnessMode.None),
                    Downmix: null,
                    SegmentNameTemplate: "audio/:codec:",
                    PlaylistNameTemplate: "audio/:codec:/playlist"
                ),
            ],
            Subtitles: []
        )
        {
            HardwarePreference = HardwarePreference.ForceSoftware,
            HlsDerivatives = new HlsDerivatives
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateMetadataJson = false,
                GenerateChapters = false,
                GenerateFontsJson = false,
            },
        };

    private static string VideoEncoderName(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H264 => "libx264",
            VideoCodecType.H265 => "libx265",
            VideoCodecType.Av1 => "libsvtav1",
            VideoCodecType.Vp9 => "libvpx-vp9",
            _ => "libx264",
        };

    private async Task<IReadOnlySet<string>> ProbeAvailableEncodersAsync()
    {
        HashSet<string> encoders = new(StringComparer.Ordinal);
        ProcessStartInfo psi = new()
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-encoders");

        using Process process = Process.Start(psi)!;
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Lines look like: " V..... libx264   H.264 ..." — the second whitespace
        // token is the encoder name.
        foreach (string line in stdout.Split('\n'))
        {
            string trimmed = line.Trim();
            string[] parts = trimmed.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (
                parts.Length >= 2
                && parts[0].Length == 6
                && parts[0].All(c => c is 'V' or 'A' or 'S' or 'F' or 'X' or 'B' or 'D' or '.')
            )
                encoders.Add(parts[1]);
        }
        return encoders;
    }

    private async Task<int> RunFfmpegAsync(string[] args)
    {
        ProcessStartInfo psi = new()
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(arg);

        using Process process = Process.Start(psi)!;
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stderr;
        await stdout;
        return process.ExitCode;
    }
}
