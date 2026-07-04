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
            .VideoOutputs.Where(v => !string.IsNullOrEmpty(v.MapLabel))
            .ToDictionary(v => v.MapLabel, _ => new VariantMetrics(5_000_000, 4_500_000));
        Dictionary<string, VariantMetrics> audioMetrics = plan
            .AudioOutputs.Where(a => !string.IsNullOrEmpty(a.MapLabel))
            .ToDictionary(a => a.MapLabel, _ => new VariantMetrics(192_000, 180_000));

        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan, MediaTitle, videoMetrics, audioMetrics);
    }

    [Fact]
    public void MasterPlaylist_WithoutSubtitles_NoSubtitleTags()
    {
        string playlist = Generate(CreatePlanWithoutSubtitles());

        playlist.Should().NotContain("TYPE=SUBTITLES");
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

        string playlist = Generate(plan);

        playlist.Should().NotContain("TYPE=SUBTITLES");
    }

    [Fact]
    public void MasterPlaylist_AudioLanguage_Correct()
    {
        string playlist = Generate(CreatePlanWithoutSubtitles());

        playlist.Should().Contain("LANGUAGE=\"eng\"");
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
