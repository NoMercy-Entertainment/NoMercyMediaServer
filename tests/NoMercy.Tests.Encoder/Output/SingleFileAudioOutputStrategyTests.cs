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
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"AudioOnly_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    // --- mp3 -----------------------------------------------------------------

    [Fact]
    public void Mp3_Configure_ForcesLameEncoderAndMuxer()
    {
        Mp3OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.flac"));

        strategy.ConfigureOutput(builder: builder, plan: Plan(format: OutputFormat.Mp3, encoder: "libmp3lame"), outputDirectory: _outputDir);

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "libmp3lame");
        args.Should().Contain(expected: "-f mp3");
        args.Should().Contain(expected: "output.mp3");
    }

    [Fact]
    public async Task Mp3_Finalize_RenamesToMediaTitle()
    {
        Mp3OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        string sourcePath = Path.Combine(path1: _outputDir, path2: "output.mp3");
        await File.WriteAllBytesAsync(path: sourcePath, bytes: [0xFF, 0xFB]);

        await strategy.FinalizeAsync(
            outputDirectory: _outputDir,
            plan: Plan(format: OutputFormat.Mp3, encoder: "libmp3lame"),
            mediaTitle: "Track-01",
            ct: CancellationToken.None
        );

        File.Exists(path: Path.Combine(path1: _outputDir, path2: "Track-01.mp3")).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: _outputDir, path2: "output.mp3")).Should().BeFalse();
    }

    // --- flac ----------------------------------------------------------------

    [Fact]
    public void Flac_Configure_ForcesFlacCodec()
    {
        FlacOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.wav"));

        strategy.ConfigureOutput(builder: builder, plan: Plan(format: OutputFormat.Flac, encoder: "flac"), outputDirectory: _outputDir);

        string args = string.Join(separator: " ", value: builder.Build(ffmpegPath: "ffmpeg").Arguments);
        args.Should().Contain(expected: "-f flac");
        args.Should().Contain(expected: "output.flac");
        (args.Contains(value: "-acodec flac") || args.Contains(value: "-c:a flac")).Should().BeTrue();
    }

    [Fact]
    public async Task Flac_Finalize_RenamesToMediaTitle()
    {
        FlacOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        await File.WriteAllBytesAsync(path: Path.Combine(path1: _outputDir, path2: "output.flac"), bytes: [0x66, 0x4C]);

        await strategy.FinalizeAsync(
            outputDirectory: _outputDir,
            plan: Plan(format: OutputFormat.Flac, encoder: "flac"),
            mediaTitle: "Song",
            ct: CancellationToken.None
        );

        File.Exists(path: Path.Combine(path1: _outputDir, path2: "Song.flac")).Should().BeTrue();
    }

    // --- ogg -----------------------------------------------------------------

    [Fact]
    public void Ogg_Configure_UsesOggMuxer()
    {
        OggOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.wav"));

        strategy.ConfigureOutput(builder: builder, plan: Plan(format: OutputFormat.Ogg, encoder: "libvorbis"), outputDirectory: _outputDir);

        string args = string.Join(separator: " ", value: builder.Build(ffmpegPath: "ffmpeg").Arguments);
        args.Should().Contain(expected: "-f ogg");
        args.Should().Contain(expected: "output.ogg");
        args.Should().Contain(expected: "libvorbis");
    }

    [Fact]
    public void Ogg_Configure_PreservesPlannerCodecChoice_Opus()
    {
        OggOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.wav"));

        strategy.ConfigureOutput(builder: builder, plan: Plan(format: OutputFormat.Ogg, encoder: "libopus"), outputDirectory: _outputDir);

        // Unlike mp3/flac, Ogg does NOT force the codec — it accepts vorbis,
        // opus, and flac. The planner's choice has to survive the strategy.
        string args = string.Join(separator: " ", value: builder.Build(ffmpegPath: "ffmpeg").Arguments);
        args.Should().Contain(expected: "libopus");
        args.Should().NotContain(unexpected: "libvorbis");
    }

    [Fact]
    public void AudioOnly_Configure_NoVideoCodecEmitted()
    {
        Mp3OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.flac"));

        strategy.ConfigureOutput(builder: builder, plan: Plan(format: OutputFormat.Mp3, encoder: "libmp3lame"), outputDirectory: _outputDir);

        string args = string.Join(separator: " ", value: builder.Build(ffmpegPath: "ffmpeg").Arguments);
        // Video codec flags should be absent — the output is audio-only.
        args.Should().NotContain(unexpected: "libx264");
        args.Should().NotContain(unexpected: "-preset");
        args.Should().NotContain(unexpected: "-crf");
    }

    [Fact]
    public void AudioOnly_NoSubdirectories_AreProduced()
    {
        Mp3OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        strategy.GetOutputSubdirectories(plan: Plan(format: OutputFormat.Mp3, encoder: "libmp3lame")).Should().BeEmpty();
    }

    private static OutputPlan Plan(OutputFormat format, string encoder) =>
        new(
            Format: format,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: encoder,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 44100,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
