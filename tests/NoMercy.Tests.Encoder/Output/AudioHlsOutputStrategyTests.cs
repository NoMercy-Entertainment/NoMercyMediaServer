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

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Audio-only HLS output strategy — emits audio.m3u8 + audio_NNN.aac with no
/// master playlist (the music player loads audio.m3u8 directly). The flags
/// are part of a wire contract with hls.js and the music player; changes here
/// can break audio playback silently.
/// </summary>
public class AudioHlsOutputStrategyTests
{
    private static AudioOutputPlan TranscodeAudio(
        string encoderName = "aac",
        int bitrateKbps = 192,
        int channels = 2,
        int sampleRate = 48000,
        string? audioFilter = null
    ) =>
        new(
            EncoderName: encoderName,
            BitrateKbps: bitrateKbps,
            Channels: channels,
            SampleRate: sampleRate,
            Action: StreamAction.Transcode,
            Language: "eng",
            MapLabel: "[a0]"
        )
        {
            AudioFilter = audioFilter,
        };

    private static OutputPlan PlanWith(AudioOutputPlan? audio, int segmentSeconds = 6) =>
        new(
            Format: OutputFormat.AudioHls,
            VideoOutputs: [],
            AudioOutputs: audio is null ? [] : [audio],
            SubtitleOutputs: [],
            Thumbnails: null
        )
        {
            SegmentDurationSeconds = segmentSeconds,
        };

    private static OutputOptions GetSingleOutput(FfmpegCommandBuilder builder)
    {
        // Build to introspect the registered outputs.
        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotBeNull();
        // The builder collects outputs; introspect via reflection-free path:
        // re-add a hook by re-running ConfigureOutput on a fresh builder.
        // Easier: snapshot through a second pass. For these tests we just
        // check argument strings end-to-end via the built command.
        return null!;
    }

    // ── Format identification ──────────────────────────────────────────────

    [Fact]
    public void Format_ReturnsAudioHls()
    {
        AudioHlsOutputStrategy strategy = new();
        strategy.Format.Should().Be(OutputFormat.AudioHls);
    }

    [Fact]
    public void GetOutputSubdirectories_Empty()
    {
        AudioHlsOutputStrategy strategy = new();
        OutputPlan plan = PlanWith(TranscodeAudio());
        strategy.GetOutputSubdirectories(plan).Should().BeEmpty();
    }

    [Fact]
    public async Task FinalizeAsync_NoOp_CompletesImmediately()
    {
        AudioHlsOutputStrategy strategy = new();
        OutputPlan plan = PlanWith(TranscodeAudio());

        await strategy.FinalizeAsync("/out", plan, "Title", CancellationToken.None);
        // Just verifying it doesn't throw.
    }

    // ── Output configuration ──────────────────────────────────────────────

    [Fact]
    public void ConfigureOutput_TranscodeAac_EmitsHlsFlagsAndAudio()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        OutputPlan plan = PlanWith(TranscodeAudio(bitrateKbps: 192));

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        args.Should().Contain("-f hls");
        args.Should().Contain("-hls_time 6");
        args.Should().Contain("-hls_playlist_type vod");
        args.Should().Contain("independent_segments");
        args.Should().Contain("audio_%03d.aac");
        args.Should().Contain("audio.m3u8");
        args.Should().Contain("aac"); // codec
        args.Should().Contain("-b:a 192k");
        args.Should().Contain("-profile:a aac_low");
    }

    [Fact]
    public void ConfigureOutput_CopyAction_DoesNotAddAacProfile()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        AudioOutputPlan audio = new(
            EncoderName: "copy",
            BitrateKbps: 0,
            Channels: 6,
            SampleRate: 48000,
            Action: StreamAction.Copy,
            Language: "eng",
            MapLabel: "[a0]"
        );
        OutputPlan plan = PlanWith(audio);

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        args.Should().Contain("-c:a copy");
        args.Should().NotContain("aac_low"); // No profile flag on copy
    }

    [Fact]
    public void ConfigureOutput_CustomSegmentDuration_FlowsThrough()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        OutputPlan plan = PlanWith(TranscodeAudio(), segmentSeconds: 10);

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");

        string.Join(" ", cmd.Arguments).Should().Contain("-hls_time 10");
    }

    [Fact]
    public void ConfigureOutput_ZeroSegmentDuration_UsesDefault()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        OutputPlan plan = PlanWith(TranscodeAudio(), segmentSeconds: 0);

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");

        // Default = 6 seconds.
        string.Join(" ", cmd.Arguments).Should().Contain("-hls_time 6");
    }

    [Fact]
    public void ConfigureOutput_AudioFilter_ThreadedThrough()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        OutputPlan plan = PlanWith(TranscodeAudio(audioFilter: "loudnorm=I=-16:LRA=11"));

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");

        string.Join(" ", cmd.Arguments).Should().Contain("loudnorm=I=-16:LRA=11");
    }

    [Fact]
    public void ConfigureOutput_NoAudio_DefaultsTo2ChannelAac()
    {
        AudioHlsOutputStrategy strategy = new();
        FfmpegCommandBuilder builder = new();
        OutputPlan plan = PlanWith(audio: null);

        strategy.ConfigureOutput(builder, plan, "/out");
        FfmpegCommand cmd = builder.Build("ffmpeg");
        string args = string.Join(" ", cmd.Arguments);

        args.Should().Contain("audio.m3u8");
        // Stereo defaults so the encoder produces something sane even from
        // an empty plan.
        args.Should().Contain("-ac 2");
    }
}
