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

using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.Commands;

public class FfmpegCommandBuilderTests
{
    [Fact]
    public void SimpleH264Encode_ProducesCorrectArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input/video.mkv"))
            .AddOutput(
                new(
                    FilePath: "/output/video.mp4",
                    VideoCodec: "libx264",
                    AudioCodec: "aac",
                    Crf: 23,
                    Preset: "medium"
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("-y");
        cmd.Arguments.Should().Contain("-hide_banner");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-i", "/input/video.mkv");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", "libx264");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:a", "aac");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-crf", "23");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-preset", "medium");
        cmd.Arguments.Should().Contain("/output/video.mp4");
    }

    [Fact]
    public void HwAccelInput_IncludesHwaccelFlags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input/video.mkv", HwAccelDevice: "cuda", HwAccelOutputFormat: "cuda"))
            .AddOutput(new(FilePath: "/output/video.mp4", VideoCodec: "h264_nvenc"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-hwaccel", "cuda");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-hwaccel_output_format", "cuda");
    }

    [Fact]
    public void FilterComplex_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .WithFilterComplex("[0:v]scale=1920:1080[v0]")
            .AddOutput(new(FilePath: "/output.mp4", VideoCodec: "libx264", MapStreams: ["[v0]"]))
            .Build("ffmpeg");

        cmd.Arguments.Should()
            .ContainInConsecutiveOrder("-filter_complex", "[0:v]scale=1920:1080[v0]");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-map", "[v0]");
    }

    [Fact]
    public void MultipleOutputs_AllIncluded()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/out1.mp4", VideoCodec: "libx264"))
            .AddOutput(new(FilePath: "/out2.mp4", VideoCodec: "libx265"))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("/out1.mp4");
        cmd.Arguments.Should().Contain("/out2.mp4");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", "libx264");
    }

    [Fact]
    public void SeekAndDuration_FormattedCorrectly()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(
                new(
                    "/input.mkv",
                    SeekTo: TimeSpan.FromSeconds(30.5),
                    Duration: TimeSpan.FromSeconds(10)
                )
            )
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-ss", "30.500");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-t", "10.000");
    }

    [Fact]
    public void GlobalOptions_ThreadsAndProbesize()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(Threads: 4, ProbeSizeBytes: 5000000))
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-threads", "4");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-probesize", "5000000");
    }

    [Fact]
    public void ExtraFlags_Included()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(
                new(
                    FilePath: "/output.mp4",
                    VideoCodec: "hevc_videotoolbox",
                    ExtraFlags: new() { ["-tag:v"] = "hvc1" }
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-tag:v", "hvc1");
    }

    [Fact]
    public void NoInputs_BuildsEmptyCommand()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder().Build("ffmpeg");

        cmd.Executable.Should().Be("ffmpeg");
        cmd.Arguments.Should().Contain("-y");
    }

    [Fact]
    public void ProgressPipe_EnabledByDefault()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-progress", "pipe:1");
    }

    [Fact]
    public void GlobalExtraFlags_EmittedAsGlobalOptionsBeforeInput()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalExtraFlags(new() { ["-max_muxing_queue_size"] = "1024" })
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-max_muxing_queue_size", "1024");

        int flagIndex = Array.IndexOf(cmd.Arguments, "-max_muxing_queue_size");
        int inputIndex = Array.IndexOf(cmd.Arguments, "-i");
        flagIndex.Should().BeLessThan(inputIndex, "global custom args belong before the -i input");
    }

    [Fact]
    public void GlobalExtraFlags_NullIsNoOp()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalExtraFlags(null)
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("-i");
    }
}
