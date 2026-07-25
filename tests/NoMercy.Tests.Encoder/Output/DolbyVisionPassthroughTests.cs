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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Verifies that the container-specific Dolby Vision tags land in the
/// ffmpeg argv when <see cref="OutputPlan.PreserveDolbyVision"/> is set.
/// Without the <c>dvh1</c> tag, Apple TV / QuickTime play DV MP4 content
/// as plain HDR10 and the effect is lost — so the check has to be explicit.
/// </summary>
public class DolbyVisionPassthroughTests
{
    [Fact]
    public void Mp4_PreserveDv_AddsDvh1CodecTag()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, Plan(true), "/output");

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-tag:v dvh1");
    }

    [Fact]
    public void Mp4_NoDv_UsesDefaultTag()
    {
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(builder, Plan(false), "/output");

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().NotContain("dvh1");
    }

    [Fact]
    public void Hls_PreserveDv_HevcVariant_OverridesHvc1WithDvh1()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            HlsPlan("libx265", true),
            "/output"
        );

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-tag:v dvh1");
        // dvh1 replaces hvc1 — both appearing in the same argv is a muxer
        // contradiction and would break playback.
        args.Should().NotContain("-tag:v hvc1");
    }

    [Fact]
    public void Hls_PreserveDv_H264Variant_NoDvTag()
    {
        HlsOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        // DV only rides HEVC. H.264 variants in the same ladder must not
        // inherit the dvh1 tag or the MP4 header becomes invalid.
        strategy.ConfigureOutput(
            builder,
            HlsPlan("libx264", true),
            "/output"
        );

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().NotContain("dvh1");
    }

    private static OutputPlan Plan(bool preserveDv) =>
        new(
            OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    3840,
                    2160,
                    "libx265",
                    22,
                    0,
                    "medium",
                    "main10",
                    "5.1",
                    true,
                    "yuv420p10le",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    "libfdk_aac",
                    192,
                    2,
                    48000,
                    StreamAction.Transcode,
                    "eng",
                    "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null,
            PreserveDolbyVision: preserveDv
        );

    private static OutputPlan HlsPlan(string encoderName, bool preserveDv) =>
        new(
            OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    3840,
                    2160,
                    encoderName,
                    22,
                    0,
                    "medium",
                    "main10",
                    "5.1",
                    true,
                    "yuv420p10le",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            PreserveDolbyVision: preserveDv
        );
}
