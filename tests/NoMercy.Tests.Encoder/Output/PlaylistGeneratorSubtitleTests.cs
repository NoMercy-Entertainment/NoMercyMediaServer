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

using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

public class PlaylistGeneratorSubtitleTests
{
    private const string MediaTitle = "Movie.Name.NoMercy";

    private string Generate(OutputPlan plan)
    {
        // Seed non-zero metrics so the variant rows render — GenerateMasterPlaylist
        // skips any variant whose measured bandwidth is zero (dead-variant guard).
        Dictionary<string, VariantMetrics> videoMetrics = plan
            .VideoOutputs.Where(predicate: v => !string.IsNullOrEmpty(value: v.MapLabel))
            .ToDictionary(
                keySelector: PlaylistGenerator.VideoVariantKey,
                elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
            );
        Dictionary<string, VariantMetrics> audioMetrics = plan
            .AudioOutputs.Where(predicate: a => !string.IsNullOrEmpty(value: a.MapLabel))
            .ToDictionary(
                keySelector: PlaylistGenerator.AudioVariantKey,
                elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
            );

        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);
    }

    [Fact]
    public void MasterPlaylist_WithoutSubtitles_NoSubtitleTags()
    {
        string playlist = Generate(plan: CreatePlanWithoutSubtitles());

        playlist.Should().NotContain(unexpected: "TYPE=SUBTITLES");
    }

    [Fact]
    public void MasterPlaylist_SubtitleWithDropAction_IsExcluded()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Drop,
                    Language: "eng",
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );

        string playlist = Generate(plan: plan);

        playlist.Should().NotContain(unexpected: "TYPE=SUBTITLES");
    }

    [Fact]
    public void MasterPlaylist_AudioLanguage_Correct()
    {
        string playlist = Generate(plan: CreatePlanWithoutSubtitles());

        playlist.Should().Contain(expected: "LANGUAGE=\"eng\"");
    }

    private static OutputPlan CreatePlanWithoutSubtitles()
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs: [BuildVideo()],
            AudioOutputs: [BuildAudio()],
            SubtitleOutputs: [],
            Thumbnails: null
        );
    }

    private static VideoOutputPlan BuildVideo() =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: 23,
            BitrateKbps: 4000,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new()
        );

    private static AudioOutputPlan BuildAudio() =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: "eng",
            MapLabel: "0:a:0"
        );
}
