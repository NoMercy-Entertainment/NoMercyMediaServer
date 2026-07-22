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
using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Regression net for the field report "legacy profiles encode in HEVC".
/// Feeds the EXACT v2 ProfileJson row stored for the migrated v1 preset
/// "1080p regular" (Codec=H264) through the real plan pipeline — real
/// CodecRegistry, CodecResolver, HardwarePreferenceResolver and
/// BitDepthPolicyResolver — on a GPU host whose SpeedIndex carries both
/// h264_nvenc and hevc_nvenc benchmarks (Fillz's GTX 1060 shape). The
/// resolved encoder must stay in the H264 family for every source type.
/// </summary>
public class LegacyProfileCodecRegressionTests
{
    private const string GpuName = "NVIDIA GeForce GTX 1060 3GB";

    [Theory]
    [InlineData(data: "hdr-4k")]
    [InlineData(data: "sdr-1080p")]
    public async Task LegacyH264Profile_OnGpuHost_NeverResolvesHevcEncoder(string sourceKind)
    {
        EncodingProfile profile = LoadStoredLegacyProfile();
        MediaInfo media = sourceKind == "hdr-4k" ? BuildHdr4KSource() : BuildSdr1080PSource();

        OutputPlan plan = await RunRealPlan(media: media, profile: profile);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        video
            .EncoderName.Should()
            .Match(
                predicate: encoderName => encoderName == "libx264" || encoderName.StartsWith("h264_"),
                because: "profile stores Codec=0 (H264); resolved encoder was {0}",
                becauseArgs: video.EncoderName
            );
    }

    [Fact]
    public async Task LegacyH264Profile_NoHardware_ResolvesLibx264()
    {
        EncodingProfile profile = LoadStoredLegacyProfile();
        MediaInfo media = BuildSdr1080PSource();

        OutputPlan plan = await RunRealPlan(media: media, profile: profile, withGpu: false);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        video.EncoderName.Should().Be(expected: "libx264");
    }

    [Fact]
    public async Task LegacyH264Profile_StaleNvencBenchmark_FallsBackToLibx264()
    {
        // Field failure shape: the persisted SpeedIndex still carries nvenc
        // measurements from an older ffmpeg build, but the current bundled
        // ffmpeg has no hardware encoders. The resolver must not emit the
        // stale handle — that produces "Unknown encoder", zero variants, and
        // a header-only master playlist.
        EncodingProfile profile = LoadStoredLegacyProfile();
        MediaInfo media = BuildSdr1080PSource();

        OutputPlan plan = await RunRealPlan(media: media, profile: profile, withGpu: true, nvencInFfmpeg: false);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);
        video.EncoderName.Should().Be(expected: "libx264");
    }

    private static EncodingProfile LoadStoredLegacyProfile()
    {
        string fixturePath = Path.Combine(paths: [AppContext.BaseDirectory, "Profiles", "V2", "Fixtures", "legacy-1080p-regular.json"]
        );

        // Same parse path as PresetResolver.Resolve: JObject → ToObject.
        JObject stored = JObject.Parse(json: File.ReadAllText(path: fixturePath));
        EncodingProfile? profile = stored.ToObject<EncodingProfile>();
        profile.Should().NotBeNull();
        profile!.Video.Should().NotBeNull();
        ((int)profile.Video!.Codec).Should().Be(expected: 0);

        return profile;
    }

    private static async Task<OutputPlan> RunRealPlan(
        MediaInfo media,
        EncodingProfile profile,
        bool withGpu = true,
        bool nvencInFfmpeg = true
    )
    {
        CodecRegistry registry = new();
        CodecResolver codecResolver = new(registry: registry);
        HardwarePreferenceResolver hardwarePreferenceResolver = new();
        BitDepthPolicyResolver bitDepthPolicyResolver = new();

        GpuDevice gpu = new(
            Vendor: GpuVendor.Nvidia,
            Name: GpuName,
            VramMb: 3072,
            MaxEncoderSessions: 3,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.HasGpu).Returns(value: withGpu);
        hardware.Setup(expression: h => h.CpuCores).Returns(value: 16);
        hardware.Setup(expression: h => h.Gpus).Returns(value: withGpu ? [gpu] : []);
        hardware
            .Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>()))
            .Returns(value: withGpu);
        hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: withGpu ? gpu : null);
        // Real hardware-encoder init probe authority: nvenc is only
        // selectable when the GPU is present AND ffmpeg actually carries the
        // nvenc encoder — mirroring what HardwareEncoderProbe would confirm
        // on Fillz's GTX 1060 host.
        hardware
            .Setup(expression: h => h.UsableHardwareEncoders)
            .Returns(
                value: withGpu && nvencInFfmpeg
                    ? new HashSet<string> { "h264_nvenc", "hevc_nvenc" }
                    : new HashSet<string>()
            );

        HashSet<string> encoders =
            withGpu && nvencInFfmpeg
                ? ["libx264", "libx265", "h264_nvenc", "hevc_nvenc", "aac", "libsvtav1"]
                : ["libx264", "libx265", "aac", "libsvtav1"];

        Mock<IFfmpegCapabilities> ffmpegCapabilities = new();
        ffmpegCapabilities.Setup(expression: c => c.AvailableEncoders).Returns(value: encoders);
        ffmpegCapabilities.Setup(expression: c => c.AvailableFilters).Returns(value: new HashSet<string>());
        ffmpegCapabilities
            .Setup(expression: c => c.HasEncoder(It.IsAny<string>()))
            .Returns(valueFunction: (string name) => encoders.Contains(item: name));
        ffmpegCapabilities.Setup(expression: c => c.HasFilter(It.IsAny<string>())).Returns(value: false);

        DateTime measuredAt = DateTime.UtcNow;
        SpeedIndex speedIndex = new(
            Measurements: new()
            {
                [key: new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: GpuName)] = new(
                    Fps: 240,
                    SpeedMultiplier: 10.0,
                    MeasuredAt: measuredAt
                ),
                [key: new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1920, DeviceName: GpuName)] = new(Fps: 190, SpeedMultiplier: 7.9, MeasuredAt: measuredAt),
                [key: new(Codec: VideoCodecType.H264, Encoder: "libx264", Width: 1920, DeviceName: null)] = new(Fps: 62, SpeedMultiplier: 2.6, MeasuredAt: measuredAt),
                [key: new(Codec: VideoCodecType.H265, Encoder: "libx265", Width: 1920, DeviceName: null)] = new(Fps: 18, SpeedMultiplier: 0.7, MeasuredAt: measuredAt),
            }
        );

        PlanStage stage = new(
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: codecResolver,
            hardware: hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: ffmpegCapabilities.Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance,
            hardwarePreferenceResolver: hardwarePreferenceResolver,
            speedIndex: speedIndex,
            bitDepthPolicyResolver: bitDepthPolicyResolver
        );

        ValidateInput input = new(Media: media, Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    private static MediaInfo BuildHdr4KSource() =>
        new(
            FilePath: "/movies/Iron.Man.(2008)/Iron.Man.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 126),
            OverallBitRateKbps: 52000,
            FileSizeBytes: 49_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 3840,
                    Height: 2160,
                    FrameRate: 23.976,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 48000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "truehd",
                    Channels: 8,
                    SampleRate: 48000,
                    BitRateKbps: 4500,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );

    // BitDepth is 10 (not the legacy profile's requested 8) so this source
    // never qualifies for PlanStage's smart-copy downgrade (an exact
    // codec/resolution/bit-depth match would stream-copy instead of
    // transcoding) — these tests exist to prove the CODEC RESOLUTION path
    // (hardware selection, stale-benchmark fallback) never picks HEVC, which
    // only runs when the source actually needs a re-encode.
    private static MediaInfo BuildSdr1080PSource() =>
        new(
            FilePath: "/movies/test/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 110),
            OverallBitRateKbps: 12000,
            FileSizeBytes: 9_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "h264",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 10000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "ac3",
                    Channels: 6,
                    SampleRate: 48000,
                    BitRateKbps: 640,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams: [],
            Chapters: []
        );
}
