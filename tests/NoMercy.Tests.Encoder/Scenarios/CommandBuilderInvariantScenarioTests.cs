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

using System.Text.RegularExpressions;
using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.Scenarios;

/// <summary>
/// Invariant scenario tests for FfmpegCommandBuilder. These tests verify structural
/// guarantees across diverse ffmpeg command shapes — they do NOT test actual encoding
/// output, but rather the mechanical correctness of argv construction.
///
/// Each scenario models a realistic encoding plan (single-rung, ladder, copy+transcode,
/// etc.) and asserts invariants that MUST hold or the command is malformed.
/// If a test fails, the builder is emitting broken ffmpeg syntax.
/// </summary>
public class CommandBuilderInvariantScenarioTests
{
    private const string FfmpegPath = "ffmpeg";
    private const string InputPath = "/media/source.mkv";
    private const string OutputPath = "/output/video.mp4";

    [Fact]
    public void SingleRungCopy_NoFilterComplex_VerifiesNoFilterGraphPads()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, VideoCodec: "copy", AudioCodec: "copy", MapStreams: ["0:v", "0:a"])
            )
            .Build(ffmpegPath: FfmpegPath);

        string argString = string.Join(separator: " ", value: cmd.Arguments);
        argString
            .Should()
            .NotContain(unexpected: "-filter_complex", because: "copy codec should not use filter_complex");

        int mapIdx = 0;
        while ((mapIdx = Array.IndexOf(array: cmd.Arguments, value: "-map", startIndex: mapIdx)) >= 0)
        {
            if ((mapIdx + 1) < cmd.Arguments.Length)
            {
                string mapValue = cmd.Arguments[mapIdx + 1];
                mapValue
                    .Should()
                    .NotStartWith(unexpected: "[", because: "copy output should not map from filter graph labels");
            }
            mapIdx++;
        }
    }

    [Fact]
    public void SingleRungCopy_CopyCodecHasNoEncoderOnlyFlags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v"]))
            .Build(ffmpegPath: FfmpegPath);

        int[] copyArgIndices = cmd
            .Arguments.Select(selector: (arg, idx) => (arg, idx))
            .Where(predicate: x => x.arg == "copy")
            .Select(selector: x => x.idx)
            .ToArray();

        copyArgIndices.Should().NotBeEmpty();

        int outputStartIdx = Array.IndexOf(array: cmd.Arguments, value: OutputPath);
        string[] argRange = cmd.Arguments.Take(count: outputStartIdx).ToArray();

        argRange
            .Should()
            .NotContain(unexpected: "-crf", because: "copy codec should not have -crf")
            .And.NotContain(unexpected: "-preset", because: "copy codec should not have -preset");
    }

    [Fact]
    public void SingleRungTranscode_VideoCodecFollowedByCodecValue()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "libx264",
                    Crf: 23,
                    Preset: "medium",
                    MapStreams: ["0:v"]
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int codecIdx = Array.IndexOf(array: cmd.Arguments, value: "-c:v");
        codecIdx.Should().BeGreaterThan(expected: -1);
        (codecIdx + 1).Should().BeLessThan(expected: cmd.Arguments.Length);
        cmd.Arguments[codecIdx + 1].Should().Be(expected: "libx264");

        cmd.Arguments.Should().Contain(expected: "-crf");
        int crfIdx = Array.IndexOf(array: cmd.Arguments, value: "-crf");
        (crfIdx + 1).Should().BeLessThan(expected: cmd.Arguments.Length);
        cmd.Arguments[crfIdx + 1].Should().Be(expected: "23");
    }

    [Fact]
    public void MultiOutputWithFilterComplex_AllPadsReferencedInFilterGraphHaveMapStreams()
    {
        string filterGraph = "[0:v]split=2[v0][v1];[v0]copy[v0_out];[v1]scale=1280:-2[v1_out]";

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .WithFilterComplex(filterGraph: filterGraph)
            .AddOutput(
                output: new(FilePath: "/output/v1.mp4", VideoCodec: "libx264", MapStreams: ["[v0_out]"], Crf: 23)
            )
            .AddOutput(
                output: new(FilePath: "/output/v2.mp4", VideoCodec: "libx264", MapStreams: ["[v1_out]"], Crf: 23)
            )
            .Build(ffmpegPath: FfmpegPath);

        MatchCollection padMatches = Regex.Matches(
            input: filterGraph,
            pattern: @"\[(\w+)\]",
            options: RegexOptions.IgnoreCase
        );
        string[] outputPads = padMatches
            .Cast<Match>()
            .Select(selector: m => m.Groups[groupnum: 1].Value)
            .Distinct()
            .ToArray();

        int mapIdx = 0;
        foreach (string pad in outputPads)
        {
            mapIdx = Array.IndexOf(array: cmd.Arguments, value: "-map", startIndex: mapIdx);
            if (mapIdx > -1 && (mapIdx + 1) < cmd.Arguments.Length)
            {
                string mapValue = cmd.Arguments[mapIdx + 1];
                if (mapValue.StartsWith(value: "[") && mapValue.EndsWith(value: "]"))
                {
                    outputPads
                        .Should()
                        .Contain(
                            expected: mapValue.Trim(trimChars: ['[', ']']),
                            because: "mapped label must be produced by -filter_complex"
                        );
                }
            }
        }
    }

    [Fact]
    public void AllFlagsWithValuesHaveFollowingValue_NoDanglingFlags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(
                options: new(
                    Overwrite: true,
                    HideBanner: false,
                    ProgressPipe: true,
                    Threads: 4,
                    AnalyzeDurationUs: 5_000_000
                )
            )
            .AddInput(input: new(FilePath: InputPath, SeekTo: TimeSpan.FromSeconds(value: 1.5)))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "libx264",
                    Crf: 23,
                    Preset: "medium",
                    Profile: "high",
                    MapStreams: ["0:v"]
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        string[] flagsWithRequiredValues =
        [
            "-threads",
            "-analyzeduration",
            "-ss",
            "-i",
            "-c:v",
            "-crf",
            "-preset",
            "-profile:v",
            "-map",
            "-progress",
        ];

        for (int i = 0; i < cmd.Arguments.Length; i++)
        {
            string arg = cmd.Arguments[i];
            if (flagsWithRequiredValues.Contains(value: arg))
            {
                (i + 1)
                    .Should()
                    .BeLessThan(
                        expected: cmd.Arguments.Length,
                        because: $"Flag {arg} at position {i} lacks a following value"
                    );
                string nextArg = cmd.Arguments[i + 1];
                nextArg
                    .Should()
                    .NotStartWith(
                        unexpected: "-",
                        because: $"Value for {arg} should not start with '-'; got '{nextArg}'"
                    );
            }
        }
    }

    [Fact]
    public void NoGlobalFlagDuplicates_SingleDashY_SingleDashProgress()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: true, HideBanner: true, ProgressPipe: true))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v"]))
            .Build(ffmpegPath: FfmpegPath);

        int yCount = cmd.Arguments.Count(predicate: arg => arg == "-y");
        yCount.Should().Be(expected: 1, because: "-y should appear exactly once");

        int progressCount = cmd.Arguments.Count(predicate: arg => arg == "-progress");
        progressCount.Should().Be(expected: 1, because: "-progress should appear exactly once");

        int hideBannerCount = cmd.Arguments.Count(predicate: arg => arg == "-hide_banner");
        hideBannerCount.Should().Be(expected: 1, because: "-hide_banner should appear exactly once");
    }

    [Fact]
    public void AudioOnlyOutput_NoVideoCodecNoHevcTags()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, AudioCodec: "aac", AudioBitrateKbps: 192, MapStreams: ["0:a"])
            )
            .Build(ffmpegPath: FfmpegPath);

        cmd.Arguments.Should()
            .NotContain(unexpected: "libx264", because: "audio-only output should not reference video codec");
        cmd.Arguments.Should().NotContain(unexpected: "-tag:v", because: "audio-only output should not use video tags");

        string argString = string.Join(separator: " ", value: cmd.Arguments);
        argString.Should().NotContain(unexpected: "hvc1", because: "audio-only should not reference hvc1");
    }

    [Fact]
    public void HevcOutputToMp4Container_IncludesHvc1Tag()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: "/output/video.mp4",
                    VideoCodec: "libx265",
                    Crf: 23,
                    MapStreams: ["0:v"],
                    ExtraFlags: new() { { "-tag:v", "hvc1" } }
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int tagVIdx = Array.IndexOf(array: cmd.Arguments, value: "-tag:v");
        tagVIdx.Should().BeGreaterThan(expected: -1, because: "-tag:v should be present for HEVC in MP4");
        cmd.Arguments[tagVIdx + 1].Should().Be(expected: "hvc1");
    }

    [Fact]
    public void DolbyVisionOutput_IncludesDvh1Tag()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "libx265",
                    Crf: 23,
                    MapStreams: ["0:v"],
                    ExtraFlags: new() { { "-tag:v", "dvh1" } }
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int dvh1Idx = Array.IndexOf(array: cmd.Arguments, value: "-tag:v");
        dvh1Idx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[dvh1Idx + 1].Should().Be(expected: "dvh1");
    }

    [Fact]
    public void CustomArgumentsProfile_GlobalExtraFlagsAppearBeforeInputs()
    {
        Dictionary<string, string> customGlobalFlags = new() { { "-hwaccel", "cuda" } };

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .WithGlobalExtraFlags(flags: customGlobalFlags)
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v"]))
            .Build(ffmpegPath: FfmpegPath);

        int hwaccelIdx = Array.IndexOf(array: cmd.Arguments, value: "-hwaccel");
        int inputIdx = Array.IndexOf(array: cmd.Arguments, value: "-i");

        hwaccelIdx.Should().BeGreaterThan(expected: -1, because: "custom -hwaccel should be present");
        hwaccelIdx.Should().BeLessThan(expected: inputIdx, because: "-hwaccel must appear before -i");
    }

    [Fact]
    public void CustomArgumentsProfile_OutputExtraFlagsAppearInOutput()
    {
        Dictionary<string, string> outputCustomFlags = new() { { "-color_primaries", "bt709" } };

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "libx264",
                    Crf: 23,
                    MapStreams: ["0:v"],
                    ExtraFlags: outputCustomFlags
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int colorPrimariesIdx = Array.IndexOf(array: cmd.Arguments, value: "-color_primaries");
        colorPrimariesIdx
            .Should()
            .BeGreaterThan(expected: -1, because: "output ExtraFlags should be emitted in the command");
        cmd.Arguments[colorPrimariesIdx + 1].Should().Be(expected: "bt709");

        int outputPathIdx = Array.IndexOf(array: cmd.Arguments, value: OutputPath);
        colorPrimariesIdx
            .Should()
            .BeLessThan(expected: outputPathIdx, because: "extra flags must appear before output path");
    }

    [Fact]
    public void BooleanExtraFlagWithEmptyValue_OmitsEmptyToken()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "copy",
                    MapStreams: ["0:v"],
                    ExtraFlags: new() { { "-an", "" } }
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int anIdx = Array.IndexOf(array: cmd.Arguments, value: "-an");
        anIdx.Should().BeGreaterThan(expected: -1, because: "boolean flag -an should be present");

        if (anIdx + 1 < cmd.Arguments.Length)
        {
            string nextArg = cmd.Arguments[anIdx + 1];
            nextArg.Should().NotBe(unexpected: "", because: "empty string value should not produce an empty token");
        }
    }

    [Fact]
    public void MultipleInputs_AllInputsHaveDashI()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: "/media/source1.mkv"))
            .AddInput(input: new(FilePath: "/media/source2.mkv"))
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v", "1:a"]))
            .Build(ffmpegPath: FfmpegPath);

        int inputCount = cmd.Arguments.Count(predicate: arg => arg == "-i");
        inputCount.Should().Be(expected: 2, because: "two inputs should produce two -i flags");
    }

    [Fact]
    public void OutputMetadataStripping_IncludesMapMetadata()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v"], StripSourceMetadata: true)
            )
            .Build(ffmpegPath: FfmpegPath);

        int mapMetadataIdx = Array.IndexOf(array: cmd.Arguments, value: "-map_metadata");
        mapMetadataIdx.Should().BeGreaterThan(expected: -1, because: "-map_metadata -1 should strip source metadata");
        cmd.Arguments[mapMetadataIdx + 1].Should().Be(expected: "-1");
    }

    [Fact]
    public void OutputStreamMetadata_AppearsBeforeOutputPath()
    {
        OutputStreamTag[] tags = [new(StreamSpecifier: "v:0", Key: "language", Value: "eng"), new(StreamSpecifier: "a:0", Key: "title", Value: "English")];

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "copy",
                    MapStreams: ["0:v", "0:a"],
                    StreamMetadata: tags
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        string argString = string.Join(separator: " ", value: cmd.Arguments);
        argString.Should().Contain(expected: "-metadata:v:0", because: "stream metadata should include language tag");

        int metadataIdx = Array.IndexOf(array: cmd.Arguments, value: "-metadata:v:0");
        int outputPathIdx = Array.IndexOf(array: cmd.Arguments, value: OutputPath);

        metadataIdx.Should().BeLessThan(expected: outputPathIdx, because: "metadata must appear before output path");
    }

    [Fact]
    public void TwPassEncoding_Pass1VideoOnlyHasStatsFile()
    {
        Dictionary<string, string> pass1Flags = new()
        {
            { "-pass", "1" },
            { "-passlogfile", "/tmp/stats" },
        };

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: "/dev/null", VideoCodec: "libx264", MapStreams: ["0:v"], ExtraFlags: pass1Flags)
            )
            .Build(ffmpegPath: FfmpegPath);

        int pass1Idx = Array.IndexOf(array: cmd.Arguments, value: "-pass");
        pass1Idx.Should().BeGreaterThan(expected: -1, because: "pass 1 should include -pass flag");
        cmd.Arguments[pass1Idx + 1].Should().Be(expected: "1");

        int passlogIdx = Array.IndexOf(array: cmd.Arguments, value: "-passlogfile");
        passlogIdx.Should().BeGreaterThan(expected: -1, because: "-passlogfile should be present");
    }

    [Fact]
    public void FilterComplexAppears_BetweenInputsAndOutputs()
    {
        string filterGraph = "[0:v]scale=1280:-2[scaled]";

        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .WithFilterComplex(filterGraph: filterGraph)
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "libx264", Crf: 23, MapStreams: ["[scaled]"]))
            .Build(ffmpegPath: FfmpegPath);

        int inputIdx = Array.IndexOf(array: cmd.Arguments, value: "-i");
        int filterComplexIdx = Array.IndexOf(array: cmd.Arguments, value: "-filter_complex");
        int mapIdx = Array.IndexOf(array: cmd.Arguments, value: "-map");

        inputIdx.Should().BeLessThan(expected: filterComplexIdx, because: "-i must come before -filter_complex");
        filterComplexIdx.Should().BeLessThan(expected: mapIdx, because: "-filter_complex must come before -map");
    }

    [Fact]
    public void CultureInvariantSeekAndDuration_NoCommaSeparators()
    {
        System.Globalization.CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: "de-DE");

            FfmpegCommand cmd = new FfmpegCommandBuilder()
                .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
                .AddInput(
                    input: new(
                        FilePath: InputPath,
                        SeekTo: TimeSpan.FromMilliseconds(milliseconds: 12_345),
                        Duration: TimeSpan.FromMilliseconds(milliseconds: 7_500)
                    )
                )
                .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "copy", MapStreams: ["0:v"]))
                .Build(ffmpegPath: FfmpegPath);

            string argString = string.Join(separator: " ", value: cmd.Arguments);
            argString.Should().Contain(expected: "12.345", because: "seek should use dot separator not comma");
            argString.Should().Contain(expected: "7.500", because: "duration should use dot separator not comma");
            argString.Should().NotContain(unexpected: "12,345", because: "de-DE comma should not appear");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void OutputPathIsAlwaysLastInItsBlock_NoArgsAfterIt()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(output: new(FilePath: OutputPath, VideoCodec: "libx264", Crf: 23, MapStreams: ["0:v"]))
            .Build(ffmpegPath: FfmpegPath);

        int outputPathIdx = Array.IndexOf(array: cmd.Arguments, value: OutputPath);
        outputPathIdx
            .Should()
            .Be(expected: cmd.Arguments.Length - 1, because: "output path should be the last argument in the command");
    }

    [Fact]
    public void MultipleOutputs_EachHasOwnPath_AtEndOfItsBlock()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .WithFilterComplex(filterGraph: "[0:v]split=2[v0][v1]")
            .AddOutput(
                output: new(FilePath: "/output/out1.mp4", VideoCodec: "libx264", Crf: 23, MapStreams: ["[v0]"])
            )
            .AddOutput(
                output: new(FilePath: "/output/out2.mp4", VideoCodec: "libx265", Crf: 24, MapStreams: ["[v1]"])
            )
            .Build(ffmpegPath: FfmpegPath);

        int path1Idx = Array.IndexOf(array: cmd.Arguments, value: "/output/out1.mp4");
        int path2Idx = Array.IndexOf(array: cmd.Arguments, value: "/output/out2.mp4");

        path1Idx.Should().BeGreaterThan(expected: -1);
        path2Idx.Should().BeGreaterThan(expected: -1);
        path1Idx.Should().BeLessThan(expected: path2Idx, because: "paths must appear in declaration order");
        path2Idx.Should().Be(expected: cmd.Arguments.Length - 1, because: "last output path must be at the end");
    }

    [Fact]
    public void AudioBitrate_ProducesDashBColonA_WithKSuffix()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, AudioCodec: "aac", AudioBitrateKbps: 192, MapStreams: ["0:a"])
            )
            .Build(ffmpegPath: FfmpegPath);

        int bitRateIdx = Array.IndexOf(array: cmd.Arguments, value: "-b:a");
        bitRateIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[bitRateIdx + 1]
            .Should()
            .Be(expected: "192k", because: "audio bitrate should include 'k' suffix");
    }

    [Fact]
    public void VideoBitrate_ProducesDashBColonV_WithKSuffix()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, VideoCodec: "libx264", VideoBitrateKbps: 5000, MapStreams: ["0:v"])
            )
            .Build(ffmpegPath: FfmpegPath);

        int bitRateIdx = Array.IndexOf(array: cmd.Arguments, value: "-b:v");
        bitRateIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[bitRateIdx + 1]
            .Should()
            .Be(expected: "5000k", because: "video bitrate should include 'k' suffix");
    }

    [Fact]
    public void ProfileAndLevel_AppearsForVideoOutput()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(
                    FilePath: OutputPath,
                    VideoCodec: "libx264",
                    Profile: "high",
                    Level: "4.1",
                    MapStreams: ["0:v"]
                )
            )
            .Build(ffmpegPath: FfmpegPath);

        int profileIdx = Array.IndexOf(array: cmd.Arguments, value: "-profile:v");
        profileIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[profileIdx + 1].Should().Be(expected: "high");

        int levelIdx = Array.IndexOf(array: cmd.Arguments, value: "-level");
        levelIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[levelIdx + 1].Should().Be(expected: "4.1");
    }

    [Fact]
    public void PixelFormat_AppearsForVideoOutput()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, VideoCodec: "libx264", PixelFormat: "yuv420p", MapStreams: ["0:v"])
            )
            .Build(ffmpegPath: FfmpegPath);

        int pixFmtIdx = Array.IndexOf(array: cmd.Arguments, value: "-pix_fmt");
        pixFmtIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[pixFmtIdx + 1].Should().Be(expected: "yuv420p");
    }

    [Fact]
    public void KeyframeInterval_AppearsForVideoOutput()
    {
        FfmpegCommand cmd = new FfmpegCommandBuilder()
            .WithGlobalOptions(options: new(Overwrite: false, HideBanner: false, ProgressPipe: false))
            .AddInput(input: new(FilePath: InputPath))
            .AddOutput(
                output: new(FilePath: OutputPath, VideoCodec: "libx264", KeyframeInterval: 120, MapStreams: ["0:v"])
            )
            .Build(ffmpegPath: FfmpegPath);

        int gIdx = Array.IndexOf(array: cmd.Arguments, value: "-g");
        gIdx.Should().BeGreaterThan(expected: -1);
        cmd.Arguments[gIdx + 1].Should().Be(expected: "120");
    }
}
