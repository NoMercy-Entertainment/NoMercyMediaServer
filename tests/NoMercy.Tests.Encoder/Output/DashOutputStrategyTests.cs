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

using System.Text;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

public class DashOutputStrategyTests
{
    [Fact]
    public void ConfigureOutput_HasDashFormat()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-f dash");
    }

    [Fact]
    public void ConfigureOutput_HasAdaptationSets()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-adaptation_sets");
    }

    [Fact]
    public void ConfigureOutput_ProducesMpdOutput()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        cmd.Arguments.Should().Contain(predicate: a => a.Contains("manifest.mpd"));
    }

    [Fact]
    public void ConfigureOutput_UsesTemplateAndTimeline()
    {
        // DASH dynamic playlists need both -use_template and -use_timeline
        // for live + on-demand support; missing either flag breaks shaka /
        // dash.js playback.
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-use_template 1");
        args.Should().Contain(expected: "-use_timeline 1");
    }

    [Fact]
    public void ConfigureOutput_AdaptationSets_GroupsVideoAndAudio()
    {
        // Adaptation set ids 0=video, 1=audio — shape verified by the
        // dash.js / shaka.player adapter selection logic.
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "id=0,streams=v id=1,streams=a");
    }

    [Fact]
    public void ConfigureOutput_SegmentNames_UseRepresentationIdPlaceholder()
    {
        // Init and media segment names use $RepresentationID$ — the DASH
        // spec placeholder ffmpeg expands per stream. Hard-coded names
        // would collide on multi-variant outputs.
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        strategy.ConfigureOutput(builder: builder, plan: CreatePlan(), outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "init_$RepresentationID$.m4s");
        args.Should().Contain(expected: "seg_$RepresentationID$_$Number%05d$.m4s");
    }

    [Fact]
    public void ConfigureOutput_RespectsCustomSegmentDuration()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with { SegmentDurationSeconds = 4 };
        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-seg_duration 4");
    }

    [Fact]
    public void ConfigureOutput_AudioCopy_EmitsCopyToken()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0")],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "-c:a copy");
    }

    [Fact]
    public void ConfigureOutput_AudioFilter_AppliedWhenTranscoding()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs =
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")
                {
                    AudioFilter = "loudnorm=I=-16",
                },
            ],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "loudnorm=I=-16");
    }

    [Fact]
    public void ConfigureOutput_DropAudio_NotMapped()
    {
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(input: new(FilePath: "/input.mkv"));

        OutputPlan plan = CreatePlan() with
        {
            AudioOutputs = [new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Drop, Language: "eng", MapLabel: "0:a:0")],
        };

        strategy.ConfigureOutput(builder: builder, plan: plan, outputDirectory: "/output");

        FfmpegCommand cmd = builder.Build(ffmpegPath: "ffmpeg");
        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().NotContain(unexpected: "-map 0:a:0");
    }

    [Fact]
    public void Format_IsDash()
    {
        new DashOutputStrategy(storage: TestStorageFactory.CreateLocal())
            .Format.Should()
            .Be(expected: OutputFormat.Dash);
    }

    [Fact]
    public void GetOutputSubdirectories_ReturnsEmpty()
    {
        new DashOutputStrategy(storage: TestStorageFactory.CreateLocal())
            .GetOutputSubdirectories(plan: CreatePlan())
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_InjectsEventStreamIntoMpd()
    {
        LocalStorage storage = TestStorageFactory.CreateLocal();
        DashOutputStrategy strategy = new(storage: storage);
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"dash_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        try
        {
            const string mpd =
                "<?xml version=\"1.0\"?><MPD xmlns=\"urn:mpeg:dash:schema:mpd:2011\"><Period></Period></MPD>";
            await storage.WriteAsync(
                path: Path.Combine(path1: dir, path2: "manifest.mpd"),
                bytes: Encoding.UTF8.GetBytes(s: mpd),
                ct: CancellationToken.None
            );

            OutputPlan plan = CreatePlan() with
            {
                Chapters =
                [
                    new(Start: TimeSpan.Zero, End: TimeSpan.FromSeconds(seconds: 300), Title: "Intro"),
                    new(Start: TimeSpan.FromSeconds(seconds: 300), End: TimeSpan.FromSeconds(seconds: 600), Title: "Main"),
                ],
            };

            await strategy.FinalizeAsync(outputDirectory: dir, plan: plan, mediaTitle: "Movie", ct: CancellationToken.None);

            byte[] bytes = await storage.ReadAsync(
                path: Path.Combine(path1: dir, path2: "Movie.mpd"),
                ct: CancellationToken.None
            );
            string xml = Encoding.UTF8.GetString(bytes: bytes);

            xml.Should().Contain(expected: "urn:nomercy:chapters");
            xml.Should().Contain(expected: "Intro").And.Contain(expected: "Main");
            // timescale=1000 → ms. First chapter spans 0..300000, second starts at 300000.
            xml.Should().Contain(expected: "presentationTime=\"0\"");
            xml.Should().Contain(expected: "duration=\"300000\"");
            xml.Should().Contain(expected: "presentationTime=\"300000\"");
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task FinalizeAsync_NoChapters_RenamesManifestWithoutEventStream()
    {
        LocalStorage storage = TestStorageFactory.CreateLocal();
        DashOutputStrategy strategy = new(storage: storage);
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"dash_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        try
        {
            const string mpd =
                "<?xml version=\"1.0\"?><MPD xmlns=\"urn:mpeg:dash:schema:mpd:2011\"><Period></Period></MPD>";
            await storage.WriteAsync(
                path: Path.Combine(path1: dir, path2: "manifest.mpd"),
                bytes: Encoding.UTF8.GetBytes(s: mpd),
                ct: CancellationToken.None
            );

            await strategy.FinalizeAsync(outputDirectory: dir, plan: CreatePlan(), mediaTitle: "Movie", ct: CancellationToken.None);

            (await storage.ExistsAsync(path: Path.Combine(path1: dir, path2: "Movie.mpd"), ct: CancellationToken.None))
                .Should()
                .BeTrue(because: "the manifest is renamed to the media title");
            byte[] bytes = await storage.ReadAsync(
                path: Path.Combine(path1: dir, path2: "Movie.mpd"),
                ct: CancellationToken.None
            );
            Encoding.UTF8.GetString(bytes: bytes).Should().NotContain(unexpected: "urn:nomercy:chapters");
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    private static OutputPlan CreatePlan() =>
        new(
            Format: OutputFormat.Dash,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 8000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
