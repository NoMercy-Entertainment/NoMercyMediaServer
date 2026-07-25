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
/// Covers Phase 13 remaining audio-only containers — mp3, flac, ogg.
/// Each produces a single <c>{title}.{ext}</c> file with no sidecars.
/// Tests verify both the generated ffmpeg command and the finalize-rename.
/// </summary>
public class SingleFileAudioOutputStrategyTests : IDisposable
{
    private readonly string _outputDir;

    public SingleFileAudioOutputStrategyTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"AudioOnly_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, true);
        GC.SuppressFinalize(this);
    }

    // --- mp3 -----------------------------------------------------------------

    [Fact]
    public void Mp3_Configure_ForcesLameEncoderAndMuxer()
    {
        Mp3OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.flac"));

        strategy.ConfigureOutput(builder, Plan(OutputFormat.Mp3, "libmp3lame"), _outputDir);

        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);
        args.Should().Contain("libmp3lame");
        args.Should().Contain("-f mp3");
        args.Should().Contain("output.mp3");
    }

    [Fact]
    public async Task Mp3_Finalize_RenamesToMediaTitle()
    {
        Mp3OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(_outputDir, "output.mp3");
        await File.WriteAllBytesAsync(sourcePath, [0xFF, 0xFB]);

        await strategy.FinalizeAsync(
            _outputDir,
            Plan(OutputFormat.Mp3, "libmp3lame"),
            "Track-01",
            CancellationToken.None
        );

        File.Exists(Path.Combine(_outputDir, "Track-01.mp3")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDir, "output.mp3")).Should().BeFalse();
    }

    // --- flac ----------------------------------------------------------------

    [Fact]
    public void Flac_Configure_ForcesFlacCodec()
    {
        FlacOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.wav"));

        strategy.ConfigureOutput(builder, Plan(OutputFormat.Flac, "flac"), _outputDir);

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-f flac");
        args.Should().Contain("output.flac");
        (args.Contains("-acodec flac") || args.Contains("-c:a flac")).Should().BeTrue();
    }

    [Fact]
    public async Task Flac_Finalize_RenamesToMediaTitle()
    {
        FlacOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        await File.WriteAllBytesAsync(Path.Combine(_outputDir, "output.flac"), [0x66, 0x4C]);

        await strategy.FinalizeAsync(
            _outputDir,
            Plan(OutputFormat.Flac, "flac"),
            "Song",
            CancellationToken.None
        );

        File.Exists(Path.Combine(_outputDir, "Song.flac")).Should().BeTrue();
    }

    // --- ogg -----------------------------------------------------------------

    [Fact]
    public void Ogg_Configure_UsesOggMuxer()
    {
        OggOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.wav"));

        strategy.ConfigureOutput(builder, Plan(OutputFormat.Ogg, "libvorbis"), _outputDir);

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("-f ogg");
        args.Should().Contain("output.ogg");
        args.Should().Contain("libvorbis");
    }

    [Fact]
    public void Ogg_Configure_PreservesPlannerCodecChoice_Opus()
    {
        OggOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.wav"));

        strategy.ConfigureOutput(builder, Plan(OutputFormat.Ogg, "libopus"), _outputDir);

        // Unlike mp3/flac, Ogg does NOT force the codec — it accepts vorbis,
        // opus, and flac. The planner's choice has to survive the strategy.
        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        args.Should().Contain("libopus");
        args.Should().NotContain("libvorbis");
    }

    [Fact]
    public void AudioOnly_Configure_NoVideoCodecEmitted()
    {
        Mp3OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.flac"));

        strategy.ConfigureOutput(builder, Plan(OutputFormat.Mp3, "libmp3lame"), _outputDir);

        string args = string.Join(" ", builder.Build("ffmpeg").Arguments);
        // Video codec flags should be absent — the output is audio-only.
        args.Should().NotContain("libx264");
        args.Should().NotContain("-preset");
        args.Should().NotContain("-crf");
    }

    [Fact]
    public void AudioOnly_NoSubdirectories_AreProduced()
    {
        Mp3OutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        strategy.GetOutputSubdirectories(Plan(OutputFormat.Mp3, "libmp3lame")).Should().BeEmpty();
    }

    private static OutputPlan Plan(OutputFormat format, string encoder) =>
        new(
            format,
            [],
            [
                new(
                    encoder,
                    192,
                    2,
                    44100,
                    StreamAction.Transcode,
                    "eng",
                    "0:a:0"
                ),
            ],
            [],
            null
        );
}
