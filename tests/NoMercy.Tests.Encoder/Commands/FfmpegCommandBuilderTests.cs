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

    // ── Audio bitrate and channel configuration ─────────────────────────────

    [Theory]
    [InlineData(192, "192k")]
    [InlineData(128, "128k")]
    [InlineData(320, "320k")]
    public void AudioBitrate_FormattedAsKbpsString(int bitrateKbps, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", AudioBitrateKbps: bitrateKbps))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-b:a", expected);
    }

    [Theory]
    [InlineData("stereo")]
    [InlineData("mono")]
    [InlineData("5.1")]
    public void AudioChannels_IncludedInArgs(string channels)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", AudioChannels: channels))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-ac", channels);
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void AudioSampleRate_IncludedInArgs(int sampleRate)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", AudioSampleRate: sampleRate))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-ar", sampleRate.ToString());
    }

    // ── Video bitrate and quality ───────────────────────────────────────────

    [Theory]
    [InlineData(4000, "4000k")]
    [InlineData(8000, "8000k")]
    [InlineData(15000, "15000k")]
    public void VideoBitrate_FormattedAsKbpsString(int bitrateKbps, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", VideoBitrateKbps: bitrateKbps))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-b:v", expected);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(23)]
    [InlineData(28)]
    [InlineData(51)]
    public void Crf_IncludedInArgs(int crfValue)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", Crf: crfValue))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-crf", crfValue.ToString());
    }

    // ── Codec and format options ────────────────────────────────────────────

    [Theory]
    [InlineData("libx264", "h264")]
    [InlineData("libx265", "hevc")]
    [InlineData("libsvtav1", "av1")]
    public void VideoCodec_IncludedInArgs(string codec, string _)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", VideoCodec: codec))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", codec);
    }

    [Theory]
    [InlineData("aac")]
    [InlineData("libopus")]
    [InlineData("ac3")]
    public void AudioCodec_IncludedInArgs(string codec)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", AudioCodec: codec))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:a", codec);
    }

    [Fact]
    public void SubtitleCodec_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", SubtitleCodec: "mov_text"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:s", "mov_text");
    }

    [Theory]
    [InlineData("yuv420p")]
    [InlineData("yuv420p10le")]
    [InlineData("yuv422p")]
    public void PixelFormat_IncludedInArgs(string format)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", PixelFormat: format))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-pix_fmt", format);
    }

    // ── Codec profile and level ────────────────────────────────────────────

    [Theory]
    [InlineData("baseline")]
    [InlineData("main")]
    [InlineData("high")]
    public void CodecProfile_IncludedInArgs(string profile)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", Profile: profile))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-profile:v", profile);
    }

    [Theory]
    [InlineData("3.1")]
    [InlineData("4.0")]
    [InlineData("5.1")]
    public void CodecLevel_IncludedInArgs(string level)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", Level: level))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-level", level);
    }

    // ── Preset and keyframe interval ────────────────────────────────────────

    [Theory]
    [InlineData("ultrafast")]
    [InlineData("fast")]
    [InlineData("medium")]
    [InlineData("slow")]
    [InlineData("veryslow")]
    public void Preset_IncludedInArgs(string preset)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", Preset: preset))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-preset", preset);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(10)]
    public void KeyframeInterval_IncludedInArgs(int gop)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", KeyframeInterval: gop))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-g", gop.ToString());
    }

    // ── Audio filter and map options ────────────────────────────────────────


    [Fact]
    public void MapStreams_IncludedInOrder()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(
                new(
                    FilePath: "/output.mp4",
                    MapStreams: ["0:v:0", "0:a:0", "0:s:0"]
                )
            )
            .Build("ffmpeg");

        int vIdx = Array.IndexOf(cmd.Arguments, "0:v:0");
        int aIdx = Array.IndexOf(cmd.Arguments, "0:a:0");
        int sIdx = Array.IndexOf(cmd.Arguments, "0:s:0");

        vIdx.Should().BeLessThan(aIdx);
        aIdx.Should().BeLessThan(sIdx);
    }

    [Fact]
    public void MultipleMapOptions_EachPrecededByMap()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(
                new(
                    FilePath: "/output.mp4",
                    MapStreams: ["0:v", "0:a:en", "0:a:fr"]
                )
            )
            .Build("ffmpeg");

        int firstMapIdx = Array.IndexOf(cmd.Arguments, "-map");
        firstMapIdx.Should().BeGreaterThanOrEqualTo(0);

        int countMaps = cmd.Arguments.Count(arg => arg == "-map");
        countMaps.Should().Be(3);
    }

    // ── Input-side seek and duration ────────────────────────────────────────

    [Theory]
    [InlineData(0.5, "0.500")]
    [InlineData(5.123, "5.123")]
    [InlineData(120.0, "120.000")]
    public void SeekTo_FormattedWith3Decimals(double seconds, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv", SeekTo: TimeSpan.FromSeconds(seconds)))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-ss", expected);
    }

    [Theory]
    [InlineData(1.0, "1.000")]
    [InlineData(60.0, "60.000")]
    [InlineData(0.1, "0.100")]
    public void Duration_FormattedWith3Decimals(double seconds, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv", Duration: TimeSpan.FromSeconds(seconds)))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-t", expected);
    }

    // ── Global options: analysis parameters ─────────────────────────────────

    [Fact]
    public void AnalyzeDurationUs_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(new(AnalyzeDurationUs: 2000000))
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-analyzeduration", "2000000");
    }

    // ── Complex real-world scenario ──────────────────────────────────────────

    [Fact]
    public void ComplexH265Transcode_AllArgsPresent()
    {
        Dictionary<string, string> extraFlags = new() { ["-tag:v"] = "hvc1" };

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(
                new(
                    Threads: 8,
                    ProbeSizeBytes: 5000000,
                    AnalyzeDurationUs: 5000000
                )
            )
            .AddInput(
                new(
                    "/input/4k.mkv",
                    HwAccelDevice: "cuda",
                    HwAccelOutputFormat: "cuda"
                )
            )
            .WithFilterComplex("[0:v]scale_cuda=1920:1080[scaled]")
            .AddOutput(
                new(
                    FilePath: "/output/1080p.mp4",
                    MapStreams: ["[scaled]", "0:a:0"],
                    VideoCodec: "hevc_nvenc",
                    AudioCodec: "aac",
                    Profile: "main",
                    Level: "4.0",
                    PixelFormat: "yuv420p",
                    VideoBitrateKbps: 5000,
                    AudioBitrateKbps: 192,
                    KeyframeInterval: 2,
                    ExtraFlags: extraFlags
                )
            )
            .Build("ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder("-threads", "8");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-probesize", "5000000");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-hwaccel", "cuda");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-filter_complex", "[0:v]scale_cuda=1920:1080[scaled]");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:v", "hevc_nvenc");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-c:a", "aac");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-profile:v", "main");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-level", "4.0");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-b:v", "5000k");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-tag:v", "hvc1");
    }

    [Fact]
    public void MultipleInputs_AllIncludedWithOptions()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input1.mkv", SeekTo: TimeSpan.FromSeconds(10)))
            .AddInput(new("/input2.mkv", Duration: TimeSpan.FromSeconds(5)))
            .AddOutput(new(FilePath: "/output.mp4"))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("/input1.mkv");
        cmd.Arguments.Should().Contain("/input2.mkv");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-ss", "10.000");
        cmd.Arguments.Should().ContainInConsecutiveOrder("-t", "5.000");
    }

    // ── Output options without mapped inputs ────────────────────────────────

    [Fact]
    public void OutputWithoutMapStreams_UsesDefault()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(new(FilePath: "/output.mp4", VideoCodec: "libx264"))
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("/output.mp4");
    }

    [Fact]
    public void AllNullOutputOptions_MinimalCommand()
    {
        OutputOptions output = new(FilePath: "/output.mp4");
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(new("/input.mkv"))
            .AddOutput(output)
            .Build("ffmpeg");

        cmd.Arguments.Should().Contain("-y");
        cmd.Arguments.Should().Contain("/output.mp4");
    }
}
