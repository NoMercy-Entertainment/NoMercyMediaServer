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
        BuiltinPresets.All().Select(preset => new object[] { preset.Name });

    [Theory]
    [MemberData(nameof(Presets))]
    public async Task BuiltinPreset_PlansWithoutThrowing(string presetName)
    {
        EncodingProfile profile = BuiltinPresets.All().Single(preset => preset.Name == presetName);
        PlanStage stage = BuildStage();

        ValidateInput input = new(RichSource(), profile);
        StageResult result = await stage.ExecuteAsync(
            input,
            EncodingContext.Create(),
            CancellationToken.None
        );

        result
            .Should()
            .BeOfType<StageSuccess<ExecutionPlan>>(
                $"builtin preset '{presetName}' must plan cleanly on a rich source"
            );

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)result).Value;
        if (profile.Video is not null && profile.Video.Policy != StreamPolicy.Omit)
            plan.OutputPlan.VideoOutputs.Should().NotBeEmpty($"'{presetName}' declares video");
    }

    private static PlanStage BuildStage()
    {
        Mock<ICodecResolver> codecResolver = new();
        Mock<IHardwareCapabilities> hardware = new();
        Mock<IFfmpegCapabilities> ffmpeg = new();

        hardware.Setup(h => h.HasGpu).Returns(false);
        hardware.Setup(h => h.CpuCores).Returns(8);
        hardware.Setup(h => h.Gpus).Returns([]);
        hardware.Setup(h => h.SupportsHardwareEncoding(It.IsAny<VideoCodecType>())).Returns(false);
        hardware.Setup(h => h.GetGpuForCodec(It.IsAny<VideoCodecType>())).Returns((GpuDevice?)null);
        ffmpeg.Setup(f => f.HasFilter(It.IsAny<string>())).Returns(true);

        codecResolver
            .Setup(r =>
                r.Resolve(
                    It.IsAny<VideoCodecType>(),
                    It.IsAny<IHardwareCapabilities>(),
                    It.IsAny<EncoderPreference>()
                )
            )
            .Returns(
                (VideoCodecType codec, IHardwareCapabilities _, EncoderPreference _) =>
                    new(
                        SoftwareEncoderFor(codec),
                        new(
                            SoftwareEncoderFor(codec),
                            null,
                            ["medium"],
                            ["main", "high", "main10"],
                            ["4.1", "5.1"],
                            new(0, 51, 23),
                            [RateControlMode.Crf],
                            true,
                            true,
                            int.MaxValue,
                            "yuv420p10le",
                            new()
                        ),
                        null,
                        RateControlMode.Crf
                    )
            );

        return new(
            new(),
            new(),
            new(),
            codecResolver.Object,
            hardware.Object,
            new TonemapSelector(),
            ffmpeg.Object,
            new AbrLadderGenerator(),
            new NoOpCropDetector(),
            NullLogger<PlanStage>.Instance
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
            "/media/rich.mkv",
            "matroska",
            TimeSpan.FromMinutes(120),
            50000,
            40_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    3840,
                    2160,
                    24.0,
                    10,
                    "yuv420p10le",
                    "bt2020",
                    "smpte2084",
                    "bt2020nc",
                    true,
                    45000
                ),
            ],
            [
                new(
                    1,
                    "eac3",
                    6,
                    48000,
                    768,
                    "eng",
                    true,
                    false
                ),
            ],
            [
                new(2, "subrip", "eng", true, false),
            ],
            []
        );
}
