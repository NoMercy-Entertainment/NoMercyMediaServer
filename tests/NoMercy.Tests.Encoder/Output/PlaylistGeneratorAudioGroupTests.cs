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

public class PlaylistGeneratorAudioGroupTests
{
    private const string MediaTitle = "Test.Title";

    private string Generate(OutputPlan plan)
    {
        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: v => VideoVariantKey(video: v),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: a => AudioVariantKey(audio: a),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);
    }

    private static string VideoVariantKey(VideoOutputPlan video) =>
        TemplateResolver.Resolve(
            template: video.PlaylistNameTemplate,
            values: TemplateResolver.VideoTokens(width: video.Width, height: video.Height, isHdrOutput: video.IsHdrOutput)
        );

    private static string AudioVariantKey(AudioOutputPlan audio) =>
        TemplateResolver.Resolve(
            template: audio.PlaylistNameTemplate,
            values: TemplateResolver.AudioTokens(language: audio.Language ?? "und", codecName: audio.CodecToken, channels: audio.Channels)
        );

    [Fact]
    public void GenerateMasterPlaylist_VideoOnlyNoAudio_OmitsAudioGroupAndAudioAttribute()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().NotContain(unexpected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().NotContain(unexpected: "AUDIO=");
        master.Should().Contain(expected: "CLOSED-CAPTIONS=NONE");
    }

    [Fact]
    public void GenerateMasterPlaylist_WithAudioRendition_EmitsAudioGroupAndAttribute()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().Contain(expected: "GROUP-ID=\"audio_aac\"");
        master.Should().Contain(expected: "LANGUAGE=\"eng\"");
        master.Should().Contain(expected: "AUDIO=\"audio_aac\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_MultipleAudioCodecs_KeepsDistinctGroupIds()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "GROUP-ID=\"audio_aac\"");
        master.Should().MatchRegex(regularExpression: @"AUDIO=""audio_aac""");
        master.Should().NotContain(unexpected: "audio_opus");
        master.Should().NotContain(unexpected: "audio_eac3");
    }

    [Fact]
    public void GenerateMasterPlaylist_OpusAudio_UsesOpusGroupId()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "libopus", BitrateKbps: 128, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "GROUP-ID=\"audio_opus\"");
        master.Should().MatchRegex(regularExpression: @"AUDIO=""audio_opus""");
    }

    [Fact]
    public void GenerateMasterPlaylist_Eac3Audio_UsesEac3GroupId()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "eac3", BitrateKbps: 384, Channels: 6, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "GROUP-ID=\"audio_eac3\"");
        master.Should().MatchRegex(regularExpression: @"AUDIO=""audio_eac3""");
    }

    [Fact]
    public void GenerateMasterPlaylist_AudioWithZeroBandwidth_OmitsFromGroupAndDoesNotEmitAudioAttribute()
    {
        PlaylistGenerator generator = new();
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Dictionary<string, VariantMetrics> vidMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000),
        };

        Dictionary<string, VariantMetrics> audMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 0, AverageBandwidth: 0),
        };

        string master = generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: vidMetrics, audioMetrics: audMetrics);

        master.Should().NotContain(unexpected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().NotContain(unexpected: "AUDIO=");
    }

    [Fact]
    public void GenerateMasterPlaylist_MultipleAudioLanguages_EachEmitsOwnMediaLine()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "fra", MapLabel: "0:a:1"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: v => VideoVariantKey(video: v),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
            [key: AudioVariantKey(audio: plan.AudioOutputs[1])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
        };

        PlaylistGenerator generator = new();
        string master = generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);

        int audioMediaCount = System.Text.RegularExpressions.Regex.Matches(
            input: master,
            pattern: "#EXT-X-MEDIA:TYPE=AUDIO"
        ).Count;
        audioMediaCount.Should().Be(expected: 2);

        master.Should().Contain(expected: "LANGUAGE=\"eng\"");
        master.Should().Contain(expected: "LANGUAGE=\"fra\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_AudioCopyAction_IncludedInGroup()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "#EXT-X-MEDIA:TYPE=AUDIO");
        master.Should().Contain(expected: "GROUP-ID=\"audio_aac\"");
        master.Should().Contain(expected: "LANGUAGE=\"eng\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_AudioOtherAction_NotIncludedInGroup()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Drop, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().NotContain(unexpected: "LANGUAGE=\"eng\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_DefaultAudioFlag_FirstRenditionOnly()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "fra", MapLabel: "0:a:1"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: v => VideoVariantKey(video: v),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = new()
        {
            [key: AudioVariantKey(audio: plan.AudioOutputs[0])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
            [key: AudioVariantKey(audio: plan.AudioOutputs[1])] = new(PeakBandwidth: 192_000, AverageBandwidth: 180_000),
        };

        PlaylistGenerator generator = new();
        string master = generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);

        int defaultCount = System.Text.RegularExpressions.Regex.Matches(input: master, pattern: "DEFAULT=YES").Count;
        defaultCount.Should().Be(expected: 1);

        master.Should().Contain(expected: "LANGUAGE=\"eng\",AUTOSELECT=YES,DEFAULT=YES");
        master.Should().Contain(expected: "LANGUAGE=\"fra\",AUTOSELECT=YES,DEFAULT=NO");
    }
}
