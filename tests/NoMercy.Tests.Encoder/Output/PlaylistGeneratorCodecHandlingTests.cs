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

public class PlaylistGeneratorCodecHandlingTests
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
    public void GenerateMasterPlaylist_H264VideoOnly_OmitsCodecsAttribute()
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

        master.Should().Contain(expected: "avc1.640028");
        master.Should().NotContain(unexpected: "CODECS=\"\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_HevcVideoOnly_IncludesHevcCodec()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx265", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "main", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "hvc1.");
    }

    [Fact]
    public void GenerateMasterPlaylist_Av1VideoOnly_IncludesAv1Codec()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libsvtav1", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: null, Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "av01.");
    }

    [Fact]
    public void GenerateMasterPlaylist_VideoAndAudio_CombinesCodecStrings()
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

        master.Should().Contain(expected: "CODECS=\"avc1.640028,mp4a.40.2\"");
    }

    [Fact]
    public void GenerateMasterPlaylist_CopyModeVideo_OmitsCodecTag()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "copy", Crf: 23, BitrateKbps: 0, Preset: null, Profile: null, Level: null, TenBit: false,
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

        master.Should().Contain(expected: "CODECS=\"mp4a.40.2\"");
        master.Should().NotContain(unexpected: "avc1");
        master.Should().NotContain(unexpected: "hvc1");
        master.Should().NotContain(unexpected: "av01");
    }

    [Fact]
    public void GenerateMasterPlaylist_CopyModeAudioOnly_OmitsCodecTag()
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
                new(EncoderName: "copy", BitrateKbps: 0, Channels: 2, SampleRate: 48000, Action: StreamAction.Copy, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "CODECS=\"avc1.640028\"");
        master.Should().NotContain(unexpected: "mp4a");
        master.Should().NotContain(unexpected: "opus");
    }

    [Fact]
    public void GenerateMasterPlaylist_AudioCodecsVary_EachVariantShowsItsCodec()
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

        Dictionary<string, VariantMetrics> videoMetrics = plan.VideoOutputs.ToDictionary(
            keySelector: v => VideoVariantKey(video: v),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 5_000_000, AverageBandwidth: 4_500_000)
        );

        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: a => AudioVariantKey(audio: a),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        string master = generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);

        master.Should().Contain(expected: "mp4a.40.2");
    }

    [Fact]
    public void GenerateMasterPlaylist_TenBitHevc_IncludesBitDepthInCodec()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx265", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "main10", Level: "4.1", TenBit: true,
                    PixelFormat: "yuv420p10le", MapLabel: "[v0]", ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        string master = Generate(plan: plan);

        master.Should().Contain(expected: "hvc1.");
        master.Should().NotContain(unexpected: "avc1");
    }

    [Fact]
    public void GenerateMasterPlaylist_MultipleVideoResolutions_BandwidthsDistinct()
    {
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920, Height: 1080, EncoderName: "libx264", Crf: 23, BitrateKbps: 8000, Preset: "medium", Profile: "high", Level: "4.0", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v0]", ExtraFlags: new()
                ),
                new(
                    Width: 1280, Height: 720, EncoderName: "libx264", Crf: 23, BitrateKbps: 4000, Preset: "medium", Profile: "high", Level: "3.1", TenBit: false,
                    PixelFormat: "yuv420p", MapLabel: "[v1]", ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0"),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Dictionary<string, VariantMetrics> videoMetrics = new()
        {
            [key: VideoVariantKey(video: plan.VideoOutputs[0])] = new(PeakBandwidth: 8_000_000, AverageBandwidth: 6_500_000),
            [key: VideoVariantKey(video: plan.VideoOutputs[1])] = new(PeakBandwidth: 3_000_000, AverageBandwidth: 2_400_000),
        };

        Dictionary<string, VariantMetrics> audioMetrics = plan.AudioOutputs.ToDictionary(
            keySelector: a => AudioVariantKey(audio: a),
            elementSelector: _ => new VariantMetrics(PeakBandwidth: 192_000, AverageBandwidth: 180_000)
        );

        PlaylistGenerator generator = new();
        string master = generator.GenerateMasterPlaylist(plan: plan, mediaTitle: MediaTitle, videoMetrics: videoMetrics, audioMetrics: audioMetrics);

        master.Should().Contain(expected: "BANDWIDTH=8192000");
        master.Should().Contain(expected: "BANDWIDTH=3192000");
    }
}
