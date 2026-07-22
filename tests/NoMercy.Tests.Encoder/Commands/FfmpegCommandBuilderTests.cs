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
            .AddInput(input: new(FilePath: "/input/video.mkv"))
            .AddOutput(
                output: new(
                    FilePath: "/output/video.mp4",
                    VideoCodec: "libx264",
                    AudioCodec: "aac",
                    Crf: 23,
                    Preset: "medium"
                )
            )
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "-y");
        cmd.Arguments.Should().Contain(expected: "-hide_banner");
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-i", "/input/video.mkv"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:v", "libx264"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:a", "aac"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-crf", "23"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-preset", "medium"]);
        cmd.Arguments.Should().Contain(expected: "/output/video.mp4");
    }

    [Fact]
    public void HwAccelInput_IncludesHwaccelFlags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input/video.mkv", HwAccelDevice: "cuda", HwAccelOutputFormat: "cuda"))
            .AddOutput(output: new(FilePath: "/output/video.mp4", VideoCodec: "h264_nvenc"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-hwaccel", "cuda"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-hwaccel_output_format", "cuda"]);
    }

    [Fact]
    public void FilterComplex_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .WithFilterComplex(filterGraph: "[0:v]scale=1920:1080[v0]")
            .AddOutput(output: new(FilePath: "/output.mp4", VideoCodec: "libx264", MapStreams: ["[v0]"]))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should()
            .ContainInConsecutiveOrder(expected: ["-filter_complex", "[0:v]scale=1920:1080[v0]"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-map", "[v0]"]);
    }

    [Fact]
    public void MultipleOutputs_AllIncluded()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/out1.mp4", VideoCodec: "libx264"))
            .AddOutput(output: new(FilePath: "/out2.mp4", VideoCodec: "libx265"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "/out1.mp4");
        cmd.Arguments.Should().Contain(expected: "/out2.mp4");
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:v", "libx264"]);
    }

    [Fact]
    public void SeekAndDuration_FormattedCorrectly()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(
                input: new(
                    FilePath: "/input.mkv",
                    SeekTo: TimeSpan.FromSeconds(value: 30.5),
                    Duration: TimeSpan.FromSeconds(seconds: 10)
                )
            )
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-ss", "30.500"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-t", "10.000"]);
    }

    [Fact]
    public void GlobalOptions_ThreadsAndProbesize()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Threads: 4, ProbeSizeBytes: 5000000))
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-threads", "4"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-probesize", "5000000"]);
    }

    [Fact]
    public void ExtraFlags_Included()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(
                output: new(
                    FilePath: "/output.mp4",
                    VideoCodec: "hevc_videotoolbox",
                    ExtraFlags: new() { [key: "-tag:v"] = "hvc1" }
                )
            )
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-tag:v", "hvc1"]);
    }

    [Fact]
    public void NoInputs_BuildsEmptyCommand()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder().Build(ffmpegPath: "ffmpeg");

        cmd.Executable.Should().Be(expected: "ffmpeg");
        cmd.Arguments.Should().Contain(expected: "-y");
    }

    [Fact]
    public void ProgressPipe_EnabledByDefault()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-progress", "pipe:1"]);
    }

    [Fact]
    public void GlobalExtraFlags_EmittedAsGlobalOptionsBeforeInput()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalExtraFlags(flags: new() { [key: "-max_muxing_queue_size"] = "1024" })
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-max_muxing_queue_size", "1024"]);

        int flagIndex = Array.IndexOf(array: cmd.Arguments, value: "-max_muxing_queue_size");
        int inputIndex = Array.IndexOf(array: cmd.Arguments, value: "-i");
        flagIndex.Should().BeLessThan(expected: inputIndex, because: "global custom args belong before the -i input");
    }

    [Fact]
    public void ExtraFlags_EmptyValue_EmitsBareFlagWithNoTrailingToken()
    {
        // "-an"/"-sn" are boolean ffmpeg flags with no value. Emitting the
        // empty string as a second argv token (the pre-fix behavior) adds a
        // stray empty argument ffmpeg treats as an unmapped output URL.
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(
                output: new(
                    FilePath: "/output.mp4",
                    ExtraFlags: new() { [key: "-an"] = "", [key: "-sn"] = "", [key: "-tag:v"] = "hvc1" }
                )
            )
            .Build(ffmpegPath: "ffmpeg");

        int anIndex = Array.IndexOf(array: cmd.Arguments, value: "-an");
        anIndex.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[anIndex + 1].Should().NotBe(unexpected: string.Empty, because: "no bare empty argv token");
        cmd.Arguments[anIndex + 1].Should().Be(expected: "-sn", because: "the next real token must follow directly");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-tag:v", "hvc1"]);
    }

    [Fact]
    public void GlobalExtraFlags_EmptyValue_EmitsBareFlagWithNoTrailingToken()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalExtraFlags(flags: new() { [key: "-vsync"] = "" })
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        int flagIndex = Array.IndexOf(array: cmd.Arguments, value: "-vsync");
        flagIndex.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[flagIndex + 1].Should().Be(expected: "-i", because: "no bare empty argv token before the input");
    }

    [Fact]
    public void GlobalExtraFlags_NullIsNoOp()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalExtraFlags(flags: null)
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "-i");
    }

    // ── Audio bitrate and channel configuration ─────────────────────────────

    [Theory]
    [InlineData(data: [192, "192k"])]
    [InlineData(data: [128, "128k"])]
    [InlineData(data: [320, "320k"])]
    public void AudioBitrate_FormattedAsKbpsString(int bitrateKbps, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", AudioBitrateKbps: bitrateKbps))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-b:a", expected]);
    }

    [Theory]
    [InlineData(data: "stereo")]
    [InlineData(data: "mono")]
    [InlineData(data: "5.1")]
    public void AudioChannels_IncludedInArgs(string channels)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", AudioChannels: channels))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-ac", channels]);
    }

    [Theory]
    [InlineData(data: 44100)]
    [InlineData(data: 48000)]
    [InlineData(data: 96000)]
    public void AudioSampleRate_IncludedInArgs(int sampleRate)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", AudioSampleRate: sampleRate))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-ar", sampleRate.ToString()]);
    }

    // ── Video bitrate and quality ───────────────────────────────────────────

    [Theory]
    [InlineData(data: [4000, "4000k"])]
    [InlineData(data: [8000, "8000k"])]
    [InlineData(data: [15000, "15000k"])]
    public void VideoBitrate_FormattedAsKbpsString(int bitrateKbps, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", VideoBitrateKbps: bitrateKbps))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-b:v", expected]);
    }

    [Theory]
    [InlineData(data: 18)]
    [InlineData(data: 23)]
    [InlineData(data: 28)]
    [InlineData(data: 51)]
    public void Crf_IncludedInArgs(int crfValue)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", Crf: crfValue))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-crf", crfValue.ToString()]);
    }

    // ── Codec and format options ────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["libx264", "h264"])]
    [InlineData(data: ["libx265", "hevc"])]
    [InlineData(data: ["libsvtav1", "av1"])]
    public void VideoCodec_IncludedInArgs(string codec, string _)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", VideoCodec: codec))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:v", codec]);
    }

    [Theory]
    [InlineData(data: "aac")]
    [InlineData(data: "libopus")]
    [InlineData(data: "ac3")]
    public void AudioCodec_IncludedInArgs(string codec)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", AudioCodec: codec))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:a", codec]);
    }

    [Fact]
    public void SubtitleCodec_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", SubtitleCodec: "mov_text"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:s", "mov_text"]);
    }

    [Theory]
    [InlineData(data: "yuv420p")]
    [InlineData(data: "yuv420p10le")]
    [InlineData(data: "yuv422p")]
    public void PixelFormat_IncludedInArgs(string format)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", PixelFormat: format))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-pix_fmt", format]);
    }

    // ── Codec profile and level ────────────────────────────────────────────

    [Theory]
    [InlineData(data: "baseline")]
    [InlineData(data: "main")]
    [InlineData(data: "high")]
    public void CodecProfile_IncludedInArgs(string profile)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", Profile: profile))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-profile:v", profile]);
    }

    [Theory]
    [InlineData(data: "3.1")]
    [InlineData(data: "4.0")]
    [InlineData(data: "5.1")]
    public void CodecLevel_IncludedInArgs(string level)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", Level: level))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-level", level]);
    }

    // ── Preset and keyframe interval ────────────────────────────────────────

    [Theory]
    [InlineData(data: "ultrafast")]
    [InlineData(data: "fast")]
    [InlineData(data: "medium")]
    [InlineData(data: "slow")]
    [InlineData(data: "veryslow")]
    public void Preset_IncludedInArgs(string preset)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", Preset: preset))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-preset", preset]);
    }

    [Theory]
    [InlineData(data: 2)]
    [InlineData(data: 4)]
    [InlineData(data: 10)]
    public void KeyframeInterval_IncludedInArgs(int gop)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", KeyframeInterval: gop))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-g", gop.ToString()]);
    }

    // ── Audio filter and map options ────────────────────────────────────────

    [Fact]
    public void MapStreams_IncludedInOrder()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", MapStreams: ["0:v:0", "0:a:0", "0:s:0"]))
            .Build(ffmpegPath: "ffmpeg");

        int vIdx = Array.IndexOf(array: cmd.Arguments, value: "0:v:0");
        int aIdx = Array.IndexOf(array: cmd.Arguments, value: "0:a:0");
        int sIdx = Array.IndexOf(array: cmd.Arguments, value: "0:s:0");

        vIdx.Should().BeLessThan(expected: aIdx);
        aIdx.Should().BeLessThan(expected: sIdx);
    }

    [Fact]
    public void MultipleMapOptions_EachPrecededByMap()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", MapStreams: ["0:v", "0:a:en", "0:a:fr"]))
            .Build(ffmpegPath: "ffmpeg");

        int firstMapIdx = Array.IndexOf(array: cmd.Arguments, value: "-map");
        firstMapIdx.Should().BeGreaterThanOrEqualTo(expected: 0);

        int countMaps = cmd.Arguments.Count(predicate: arg => arg == "-map");
        countMaps.Should().Be(expected: 3);
    }

    // ── Input-side seek and duration ────────────────────────────────────────

    [Theory]
    [InlineData(data: [0.5, "0.500"])]
    [InlineData(data: [5.123, "5.123"])]
    [InlineData(data: [120.0, "120.000"])]
    public void SeekTo_FormattedWith3Decimals(double seconds, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv", SeekTo: TimeSpan.FromSeconds(value: seconds)))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-ss", expected]);
    }

    [Theory]
    [InlineData(data: [1.0, "1.000"])]
    [InlineData(data: [60.0, "60.000"])]
    [InlineData(data: [0.1, "0.100"])]
    public void Duration_FormattedWith3Decimals(double seconds, string expected)
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv", Duration: TimeSpan.FromSeconds(value: seconds)))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-t", expected]);
    }

    // ── Global options: analysis parameters ─────────────────────────────────

    [Fact]
    public void AnalyzeDurationUs_IncludedInArgs()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(AnalyzeDurationUs: 2000000))
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-analyzeduration", "2000000"]);
    }

    // ── Complex real-world scenario ──────────────────────────────────────────

    [Fact]
    public void ComplexH265Transcode_AllArgsPresent()
    {
        Dictionary<string, string> extraFlags = new() { [key: "-tag:v"] = "hvc1" };

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Threads: 8, ProbeSizeBytes: 5000000, AnalyzeDurationUs: 5000000))
            .AddInput(input: new(FilePath: "/input/4k.mkv", HwAccelDevice: "cuda", HwAccelOutputFormat: "cuda"))
            .WithFilterComplex(filterGraph: "[0:v]scale_cuda=1920:1080[scaled]")
            .AddOutput(
                output: new(
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
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-threads", "8"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-probesize", "5000000"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-hwaccel", "cuda"]);
        cmd.Arguments.Should()
            .ContainInConsecutiveOrder(expected: ["-filter_complex", "[0:v]scale_cuda=1920:1080[scaled]"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:v", "hevc_nvenc"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-c:a", "aac"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-profile:v", "main"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-level", "4.0"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-b:v", "5000k"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-tag:v", "hvc1"]);
    }

    [Fact]
    public void MultipleInputs_AllIncludedWithOptions()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input1.mkv", SeekTo: TimeSpan.FromSeconds(seconds: 10)))
            .AddInput(input: new(FilePath: "/input2.mkv", Duration: TimeSpan.FromSeconds(seconds: 5)))
            .AddOutput(output: new(FilePath: "/output.mp4"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "/input1.mkv");
        cmd.Arguments.Should().Contain(expected: "/input2.mkv");
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-ss", "10.000"]);
        cmd.Arguments.Should().ContainInConsecutiveOrder(expected: ["-t", "5.000"]);
    }

    // ── Output options without mapped inputs ────────────────────────────────

    [Fact]
    public void OutputWithoutMapStreams_UsesDefault()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: new(FilePath: "/output.mp4", VideoCodec: "libx264"))
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "/output.mp4");
    }

    [Fact]
    public void AllNullOutputOptions_MinimalCommand()
    {
        OutputOptions output = new(FilePath: "/output.mp4");
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .AddInput(input: new(FilePath: "/input.mkv"))
            .AddOutput(output: output)
            .Build(ffmpegPath: "ffmpeg");

        cmd.Arguments.Should().Contain(expected: "-y");
        cmd.Arguments.Should().Contain(expected: "/output.mp4");
    }
}
