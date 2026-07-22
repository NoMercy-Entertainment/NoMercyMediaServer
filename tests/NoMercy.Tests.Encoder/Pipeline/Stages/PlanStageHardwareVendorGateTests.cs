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
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderConfig = NoMercy.Encoder.Profiles.LadderConfig;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using LadderRung = NoMercy.Encoder.Profiles.LadderRung;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Regression net for the field report "encoder-gpu queue permanently
/// jammed": ffmpeg's NoMercy fork advertises every hardware encoder it was
/// compiled with (h264_nvenc, h264_amf, h264_qsv, ...) via
/// <see cref="IFfmpegCapabilities.AvailableEncoders"/> regardless of which
/// GPU vendor is actually installed. Without a physical-presence gate,
/// <see cref="HardwarePreferenceResolver"/>'s unmeasured-encoder fallback
/// (empty <see cref="SpeedIndex"/> — the exact shape of a preset that has
/// never been benchmarked, e.g. h264_amf on a host that has never had an AMD
/// GPU) picks the FIRST codec-matching hardware name in the list — which can
/// be a vendor with no physical device at all. That resolved encoder becomes
/// an unsatisfiable <c>ResourceRequirement.GpuDeviceKey</c> that can never be
/// granted at the resource-budget gate (Fillz's 7 stuck <c>h264_amf</c> child
/// jobs on an NVIDIA-only GTX 1060 host).
///
/// <see cref="PlanStage"/> must filter <c>availableEncoderNames</c> down to
/// encoders whose required vendor has a physically detected GPU before
/// handing the list to the resolver.
/// </summary>
public class PlanStageHardwareVendorGateTests
{
    private static async Task<OutputPlan> RunPlan(bool nvidiaGpuPresent)
    {
        CodecRegistry registry = new();
        CodecResolver codecResolver = new(registry: registry);
        HardwarePreferenceResolver hardwarePreferenceResolver = new();
        BitDepthPolicyResolver bitDepthPolicyResolver = new();

        GpuDevice nvidiaGpu = new(
            Vendor: GpuVendor.Nvidia,
            Name: "NVIDIA GeForce GTX 1060 3GB",
            VramMb: 3072,
            MaxEncoderSessions: 3,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
        );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(expression: h => h.HasGpu).Returns(value: nvidiaGpuPresent);
        hardware.Setup(expression: h => h.CpuCores).Returns(value: 16);
        hardware.Setup(expression: h => h.Gpus).Returns(value: nvidiaGpuPresent ? [nvidiaGpu] : []);
        hardware
            .Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>()))
            .Returns(value: nvidiaGpuPresent);
        hardware
            .Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(value: nvidiaGpuPresent ? nvidiaGpu : null);
        // The authoritative gate: only encoders that survived the real
        // hardware-encoder init probe (HardwareEncoderProbe) are selectable.
        // A physically detected NVIDIA GPU only makes h264_nvenc/hevc_nvenc
        // probe-usable — h264_amf/h264_qsv are never usable on this host
        // regardless of what ffmpeg's compiled-in encoder list advertises.
        hardware
            .Setup(expression: h => h.UsableHardwareEncoders)
            .Returns(
                value: nvidiaGpuPresent
                    ? new HashSet<string> { "h264_nvenc", "hevc_nvenc" }
                    : new HashSet<string>()
            );

        // The NoMercy ffmpeg fork's binary capability list — every hardware
        // encoder it can build, independent of which GPU vendor is present.
        // This is the exact shape that produced Fillz's stuck AMF jobs.
        HashSet<string> encoders =
        [
            "libx264",
            "libx265",
            "h264_nvenc",
            "hevc_nvenc",
            "h264_amf",
            "hevc_amf",
            "h264_qsv",
            "hevc_qsv",
            "aac",
        ];

        Mock<IFfmpegCapabilities> ffmpegCapabilities = new();
        ffmpegCapabilities.Setup(expression: c => c.AvailableEncoders).Returns(value: encoders);
        ffmpegCapabilities.Setup(expression: c => c.AvailableFilters).Returns(value: new HashSet<string>());
        ffmpegCapabilities
            .Setup(expression: c => c.HasEncoder(It.IsAny<string>()))
            .Returns(valueFunction: (string encoderName) => encoders.Contains(item: encoderName));
        ffmpegCapabilities.Setup(expression: c => c.HasFilter(It.IsAny<string>())).Returns(value: false);

        // Empty SpeedIndex — no encoder has ever been benchmarked on this
        // host. This is the "unmeasured" branch: the resolver falls through
        // to picking a codec-matching name straight out of availableEncoders.
        SpeedIndex speedIndex = new(Measurements: new());

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

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
            Name: "Test 1080p H264",
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: [],
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new LadderRung(
                        Width: 1920,
                        Height: 1080,
                        Codec: VideoCodecType.H264,
                        BitrateKbps: 4000,
                        MaxBitrateKbps: 6000,
                        BufferSizeKbps: 8000,
                        Framerate: 24.0,
                        Preset: "medium",
                        CodecProfile: CodecProfile.High,
                        BitDepth: 8,
                        PixelFormat: "yuv420p"
                    ),
                ],
            }
        );

        MediaInfo media = new(
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
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    // Below the ladder rung's 4000 kbps target so
                    // PlanStage.ApplySmartCopyDowngrade never fires here — this
                    // suite exists to pin hardware-encoder SELECTION, which
                    // smart-copy would bypass entirely (Policy becomes Copy
                    // before any codec/hardware resolution runs).
                    BitRateKbps: 3000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

        ValidateInput input = new(Media: media, Profile: profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input: input, context: context, ct: CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(@object: result);
        return success.Value.OutputPlan;
    }

    [Fact]
    public async Task NvidiaOnlyHost_NeverResolvesAmfOrQsv()
    {
        OutputPlan plan = await RunPlan(nvidiaGpuPresent: true);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);

        video.EncoderName.Should().Be(expected: "h264_nvenc");
        video.EncoderName.Should().NotBe(unexpected: "h264_amf");
        video.EncoderName.Should().NotBe(unexpected: "h264_qsv");
    }

    [Fact]
    public async Task NoGpuAtAllHost_FallsBackToSoftware()
    {
        OutputPlan plan = await RunPlan(nvidiaGpuPresent: false);

        VideoOutputPlan video = Assert.Single(collection: plan.VideoOutputs);

        video.EncoderName.Should().Be(expected: "libx264");
    }
}
