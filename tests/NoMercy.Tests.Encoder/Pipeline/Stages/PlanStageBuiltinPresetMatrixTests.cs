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
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Codecs.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// Matrix safety net over the whole customization surface: every builtin preset
/// must produce an ExecutionPlan (never throw, never a hard failure) against a
/// rich HDR source with audio + subtitle streams. A regression in any single
/// preset's definition — bad codec/container pairing, ladder, HDR policy — trips
/// exactly one case here instead of slipping through single-axis tests.
/// </summary>
public class PlanStageBuiltinPresetMatrixTests
{
    public static IEnumerable<object[]> Presets() =>
        BuiltinPresets.All().Select(selector: preset => new object[] { preset.Name });

    [Theory]
    [MemberData(memberName: nameof(Presets))]
    public async Task BuiltinPreset_PlansWithoutThrowing(string presetName)
    {
        EncodingProfile profile = BuiltinPresets.All().Single(predicate: preset => preset.Name == presetName);
        PlanStage stage = BuildStage();

        ValidateInput input = new(Media: RichSource(), Profile: profile);
        StageResult result = await stage.ExecuteAsync(
            input: input,
            context: EncodingContext.Create(),
            ct: CancellationToken.None
        );

        result
            .Should()
            .BeOfType<StageSuccess<ExecutionPlan>>(
                because: $"builtin preset '{presetName}' must plan cleanly on a rich source"
            );

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        if (profile.Video is not null && profile.Video.Policy != StreamPolicy.Omit)
            plan.OutputPlan.VideoOutputs.Should().NotBeEmpty(because: $"'{presetName}' declares video");
    }

    private static PlanStage BuildStage()
    {
        Mock<ICodecResolver> codecResolver = new();
        Mock<IHardwareCapabilities> hardware = new();
        Mock<IFfmpegCapabilities> ffmpeg = new();

        hardware.Setup(expression: h => h.HasGpu).Returns(value: false);
        hardware.Setup(expression: h => h.CpuCores).Returns(value: 8);
        hardware.Setup(expression: h => h.Gpus).Returns(value: []);
        hardware.Setup(expression: h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(value: false);
        hardware.Setup(expression: h => h.GetGpuForCodec(It.IsAny<VideoCodecType>())).Returns(value: (GpuDevice?)null);
        ffmpeg.Setup(expression: f => f.HasFilter(It.IsAny<string>())).Returns(value: true);

        codecResolver
            .Setup(expression: r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                valueFunction: (VideoCodecType codec, IHardwareCapabilities _, EncoderPreference _) =>
                    new(
                        FfmpegEncoderName: SoftwareEncoderFor(codec: codec),
                        EncoderInfo: new(
                            FfmpegName: SoftwareEncoderFor(codec: codec),
                            RequiredVendor: null,
                            Presets: ["medium"],
                            Profiles: ["main", "high", "main10"],
                            Levels: ["4.1", "5.1"],
                            QualityRange: new(Min: 0, Max: 51, Default: 23),
                            SupportedRateControl: [RateControlMode.Crf],
                            Supports10Bit: true,
                            SupportsHdr: true,
                            MaxConcurrentSessions: int.MaxValue,
                            PixelFormat10Bit: "yuv420p10le",
                            VendorSpecificFlags: new()
                        ),
                        Device: null,
                        DefaultRateControl: RateControlMode.Crf
                    )
            );

        return new(
            graphBuilder: new(),
            groupingStrategy: new(),
            costEstimator: new(),
            codecResolver: codecResolver.Object,
            hardware: hardware.Object,
            tonemapSelector: new TonemapSelector(),
            ffmpegCapabilities: ffmpeg.Object,
            abrLadderGenerator: new AbrLadderGenerator(),
            cropDetector: new NoOpCropDetector(),
            logger: NullLogger<PlanStage>.Instance
        );
    }

    private static string SoftwareEncoderFor(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H265 => "libx265",
            VideoCodecType.Av1 => "libsvtav1",
            VideoCodecType.Vp9 => "libvpx-vp9",
            VideoCodecType.Copy => "copy",
            _ => "libx264",
        };

    private static MediaInfo RichSource() =>
        new(
            FilePath: "/media/rich.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(minutes: 120),
            OverallBitRateKbps: 50000,
            FileSizeBytes: 40_000_000_000,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 3840,
                    Height: 2160,
                    FrameRate: 24.0,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 45000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "eac3",
                    Channels: 6,
                    SampleRate: 48000,
                    BitRateKbps: 768,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams:
            [
                new(Index: 2, Codec: "subrip", Language: "eng", IsDefault: true, IsForced: false),
            ],
            Chapters: []
        );
}
