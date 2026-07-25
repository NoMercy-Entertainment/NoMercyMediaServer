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
using NoMercy.Encoder.Pipeline.Stages;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
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
        CodecResolver codecResolver = new(registry);
        HardwarePreferenceResolver hardwarePreferenceResolver = new();
        BitDepthPolicyResolver bitDepthPolicyResolver = new();

        GpuDevice nvidiaGpu = new(
            GpuVendor.Nvidia,
            "NVIDIA GeForce GTX 1060 3GB",
            3072,
            3,
            [VideoCodecType.H264, VideoCodecType.H265]
        );

        Mock<IHardwareCapabilities> hardware = new();
        hardware.Setup(h => h.HasGpu).Returns(nvidiaGpuPresent);
        hardware.Setup(h => h.CpuCores).Returns(16);
        hardware.Setup(h => h.Gpus).Returns(nvidiaGpuPresent ? [nvidiaGpu] : []);
        hardware
            .Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>()))
            .Returns(nvidiaGpuPresent);
        hardware
            .Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>()))
            .Returns(nvidiaGpuPresent ? nvidiaGpu : null);
        // The authoritative gate: only encoders that survived the real
        // hardware-encoder init probe (HardwareEncoderProbe) are selectable.
        // A physically detected NVIDIA GPU only makes h264_nvenc/hevc_nvenc
        // probe-usable — h264_amf/h264_qsv are never usable on this host
        // regardless of what ffmpeg's compiled-in encoder list advertises.
        hardware
            .Setup(h => h.UsableHardwareEncoders)
            .Returns(
                nvidiaGpuPresent
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
        ffmpegCapabilities.Setup(c => c.AvailableEncoders).Returns(encoders);
        ffmpegCapabilities.Setup(c => c.AvailableFilters).Returns(new HashSet<string>());
        ffmpegCapabilities
            .Setup(c => c.HasEncoder(It.IsAny<string>()))
            .Returns((string encoderName) => encoders.Contains(encoderName));
        ffmpegCapabilities.Setup(c => c.HasFilter(It.IsAny<string>())).Returns(false);

        // Empty SpeedIndex — no encoder has ever been benchmarked on this
        // host. This is the "unmeasured" branch: the resolver falls through
        // to picking a codec-matching name straight out of availableEncoders.
        SpeedIndex speedIndex = new(new());

        PlanStage stage = new(
            new(),
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
            Ulid.NewUlid(),
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
                        1920,
                        1080,
                        VideoCodecType.H264,
                        4000,
                        6000,
                        8000,
                        24.0,
                        "medium",
                        CodecProfile.High,
                        8,
                        "yuv420p"
                    ),
                ],
            }
        );

        MediaInfo media = new(
            "/movies/test/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(110),
            12000,
            9_000_000_000,
            [
                new(
                    0,
                    "h264",
                    1920,
                    1080,
                    24.0,
                    8,
                    "yuv420p",
                    "bt709",
                    "bt709",
                    "bt709",
                    true,
                    // Below the ladder rung's 4000 kbps target so
                    // PlanStage.ApplySmartCopyDowngrade never fires here — this
                    // suite exists to pin hardware-encoder SELECTION, which
                    // smart-copy would bypass entirely (Policy becomes Copy
                    // before any codec/hardware resolution runs).
                    3000
                ),
            ],
            [],
            [],
            []
        );

        ValidateInput input = new(media, profile);
        EncodingContext context = EncodingContext.Create();
        StageResult result = await stage.ExecuteAsync(input, context, CancellationToken.None);

        StageSuccess<ExecutionPlan> success = Assert.IsType<StageSuccess<ExecutionPlan>>(result);
        return success.Value.OutputPlan;
    }

    [Fact]
    public async Task NvidiaOnlyHost_NeverResolvesAmfOrQsv()
    {
        OutputPlan plan = await RunPlan(true);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);

        video.EncoderName.Should().Be("h264_nvenc");
        video.EncoderName.Should().NotBe("h264_amf");
        video.EncoderName.Should().NotBe("h264_qsv");
    }

    [Fact]
    public async Task NoGpuAtAllHost_FallsBackToSoftware()
    {
        OutputPlan plan = await RunPlan(false);

        VideoOutputPlan video = Assert.Single(plan.VideoOutputs);

        video.EncoderName.Should().Be("libx264");
    }
}
