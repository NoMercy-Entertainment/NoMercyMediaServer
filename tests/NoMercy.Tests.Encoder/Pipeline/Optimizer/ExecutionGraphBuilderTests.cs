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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Optimizer;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using LadderMode = NoMercy.Encoder.Profiles.LadderMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitleOutput = NoMercy.Encoder.Profiles.SubtitleOutput;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;
using ThumbnailOutput = NoMercy.Encoder.Profiles.ThumbnailOutput;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class ExecutionGraphBuilderTests
{
    private static readonly ExecutionGraphBuilder Builder = new();

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static VideoStreamInfo Sdr1080p =>
        new(
            0,
            "h264",
            1920,
            1080,
            24,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            8000
        );

    private static VideoStreamInfo Hdr4K =>
        new(
            0,
            "hevc",
            3840,
            2160,
            24,
            10,
            "yuv420p10le",
            "bt2020",
            "smpte2084",
            "bt2020nc",
            true,
            40000
        );

    private static AudioStreamInfo DefaultAudio =>
        new(
            1,
            "aac",
            2,
            48000,
            192,
            "eng",
            true,
            false
        );

    private static SubtitleStreamInfo EnglishSub =>
        new(2, "subrip", "eng", false, false);

    private static SubtitleStreamInfo FrenchSub =>
        new(3, "subrip", "fra", false, false);

    private static ChapterInfo Chapter =>
        new(TimeSpan.Zero, TimeSpan.FromMinutes(90), "Main Feature");

    private static MediaInfo MakeMedia(
        IReadOnlyList<VideoStreamInfo>? video = null,
        IReadOnlyList<AudioStreamInfo>? audio = null,
        IReadOnlyList<SubtitleStreamInfo>? subs = null,
        IReadOnlyList<ChapterInfo>? chapters = null
    ) =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            10000,
            8_000_000_000,
            video ?? [],
            audio ?? [DefaultAudio],
            subs ?? [],
            chapters ?? []
        );

    private static VideoOutput SingleOutput1080pH264 =>
        new(
            StreamPolicy.Transcode,
            VideoCodecType.H264,
            1920,
            1080,
            V2RateControlMode.Crf,
            23,
            4000,
            null,
            null,
            "fast",
            CodecProfile.High,
            null,
            null,
            8,
            null,
            2,
            false,
            "video/{label}",
            "video/{label}/playlist"
        );

    private static AudioOutput DefaultAudioOutput =>
        new(
            StreamPolicy.Transcode,
            AudioCodecType.Aac,
            192,
            2,
            48000,
            [],
            null,
            null,
            null,
            "audio/{lang}-{codec}",
            "audio/{lang}-{codec}/playlist"
        );

    private static EncodingProfile SingleOutputProfile =>
        new(
            Ulid.NewUlid(),
            "Test",
            Container.HlsTs,
            SingleOutput1080pH264,
            [DefaultAudioOutput],
            []
        );

    private static EncoderInfo MakeEncoderInfo(string name, bool isHw) =>
        new(
            name,
            isHw ? GpuVendor.Nvidia : null,
            ["fast", "medium", "slow"],
            [],
            [],
            new(0, 51, 23),
            [RateControlMode.Crf],
            false,
            false,
            isHw ? 12 : int.MaxValue,
            "yuv420p10le",
            new()
        );

    private static ResolvedCodec H264Software =>
        new(
            "libx264",
            MakeEncoderInfo("libx264", false),
            null,
            RateControlMode.Crf
        );

    private static ResolvedCodec H264Nvenc =>
        new(
            "h264_nvenc",
            MakeEncoderInfo("h264_nvenc", true),
            new(
                GpuVendor.Nvidia,
                "RTX 4090",
                24576,
                12,
                [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
            ),
            RateControlMode.Cq
        );

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public void Simple1080pH264SingleOutput_HasDecodeAndEncodeNodes()
    {
        MediaInfo media = MakeMedia([Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.Decode);
        nodes.Should().Contain(n => n.Operation == OperationType.Encode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Tonemap);
        nodes.Should().NotContain(n => n.Operation == OperationType.Split);
    }

    [Fact]
    public void Simple1080pH264SingleOutput_SameResolution_NoScaleNode()
    {
        MediaInfo media = MakeMedia([Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.Scale);
    }

    [Fact]
    public void Simple1080pH264SingleOutput_EncodeNodeHasCorrectParameters()
    {
        MediaInfo media = MakeMedia([Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        ExecutionNode encode = nodes.Single(n => n.Operation == OperationType.Encode);
        encode.Parameters["encoder"].Should().Be("libx264");
        encode.Parameters["crf"].Should().Be("23");
        encode.Parameters["preset"].Should().Be("fast");
    }

    [Fact]
    public void Hdr4KMultiResolution_HasDecodeTonemapSplitScaleEncodeChain()
    {
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            Name: "HDR Multi",
            Container: Container.HlsTs,
            Video: new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                V2RateControlMode.Crf,
                22,
                4000,
                6000,
                8000,
                "medium",
                CodecProfile.Main10,
                null,
                null,
                8,
                null,
                2,
                true,
                "video/{label}",
                "video/{label}/playlist"
            ),
            Audio: [DefaultAudioOutput],
            Subtitles: [],
            Ladder: new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(
                        1920,
                        1080,
                        VideoCodecType.H265,
                        4000,
                        6000,
                        8000,
                        24.0,
                        "medium",
                        CodecProfile.Main10,
                        8,
                        null
                    ),
                    new(
                        1280,
                        720,
                        VideoCodecType.H265,
                        2500,
                        4000,
                        5000,
                        24.0,
                        "medium",
                        CodecProfile.Main10,
                        8,
                        null
                    ),
                    new(
                        854,
                        480,
                        VideoCodecType.H265,
                        1200,
                        2000,
                        2500,
                        24.0,
                        "medium",
                        CodecProfile.Main10,
                        8,
                        null
                    ),
                ],
            }
        );

        ResolvedCodec[] resolvedCodecs =
        [
            new("hevc_nvenc", MakeEncoderInfo("hevc_nvenc", true), null, RateControlMode.Cq),
            new("hevc_nvenc", MakeEncoderInfo("hevc_nvenc", true), null, RateControlMode.Cq),
            new("hevc_nvenc", MakeEncoderInfo("hevc_nvenc", true), null, RateControlMode.Cq),
        ];

        MediaInfo media = MakeMedia([Hdr4K]);
        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, resolvedCodecs);

        nodes.Should().Contain(n => n.Operation == OperationType.Decode);
        nodes.Should().Contain(n => n.Operation == OperationType.Tonemap);
        nodes.Should().Contain(n => n.Operation == OperationType.Split);
        nodes.Count(n => n.Operation == OperationType.Scale).Should().Be(3);
        nodes.Count(n => n.Operation == OperationType.Encode).Should().Be(3);
    }

    [Fact]
    public void AudioOnlyInput_HasAudioDecodeAndEncodeOnly()
    {
        MediaInfo media = MakeMedia([], [DefaultAudio]);
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Audio Only",
            Container.HlsTs,
            null,
            [DefaultAudioOutput],
            []
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, []);

        nodes.Should().Contain(n => n.Operation == OperationType.AudioDecode);
        nodes.Should().Contain(n => n.Operation == OperationType.AudioEncode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Decode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Encode);
    }

    [Fact]
    public void MultiSubtitleInput_SubtitleExtractNodesArePresentAndIndependentOfVideoChain()
    {
        MediaInfo media = MakeMedia([Sdr1080p], subs: [EnglishSub, FrenchSub]);
        SubtitleOutput subOutput = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            true,
            null,
            "subs/{lang}"
        );
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Subs",
            Container.HlsTs,
            SingleOutput1080pH264,
            [DefaultAudioOutput],
            [subOutput, subOutput]
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        IEnumerable<ExecutionNode> subNodes = nodes.Where(n =>
            n.Operation == OperationType.SubtitleExtract
        );
        subNodes.Should().HaveCount(2);

        // Subtitle nodes must have no dependencies on video nodes
        ExecutionNode decodeNode = nodes.Single(n => n.Operation == OperationType.Decode);
        foreach (ExecutionNode subNode in subNodes)
        {
            subNode.DependsOn.Should().NotContain(decodeNode.Id);
        }
    }

    [Fact]
    public void ProfileWithThumbnails_ThumbnailCaptureNodePresent()
    {
        MediaInfo media = MakeMedia([Sdr1080p]);
        ThumbnailOutput thumbnails = new(320, 10);
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "With Thumbs",
            Container.HlsTs,
            SingleOutput1080pH264,
            [],
            [],
            thumbnails
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        ExecutionNode thumbNode = nodes.Single(n => n.Operation == OperationType.ThumbnailCapture);
        thumbNode.Parameters["width"].Should().Be("320");
        thumbNode.Parameters["interval"].Should().Be("10");
    }

    [Fact]
    public void ProfileWithoutThumbnails_NoThumbnailCaptureNode()
    {
        MediaInfo media = MakeMedia([Sdr1080p]);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.ThumbnailCapture);
    }

    [Fact]
    public void ChaptersPresent_ChapterExtractNodeAdded()
    {
        MediaInfo media = MakeMedia([Sdr1080p], chapters: [Chapter]);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.ChapterExtract);
    }

    [Fact]
    public void NoChapters_NoChapterExtractNode()
    {
        MediaInfo media = MakeMedia([Sdr1080p], chapters: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.ChapterExtract);
    }

    [Fact]
    public void AllNodes_HaveUniqueIds()
    {
        MediaInfo media = MakeMedia(
            [Sdr1080p],
            subs: [EnglishSub, FrenchSub],
            chapters: [Chapter]
        );
        SubtitleOutput subOutput = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            true,
            null,
            "subs/{lang}"
        );
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Full",
            Container.HlsTs,
            SingleOutput1080pH264,
            [DefaultAudioOutput],
            [subOutput, subOutput],
            new(320, 10)
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        IEnumerable<string> ids = nodes.Select(n => n.Id);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SingleOutputSmallerResolution_ScaleNodeAdded()
    {
        // 4K source encoded to 1080p → needs Scale node
        VideoStreamInfo source4k = Hdr4K with
        {
            ColorPrimaries = "bt709",
            ColorTransfer = "bt709",
            ColorSpace = "bt709",
        };
        MediaInfo media = MakeMedia([source4k]);
        EncodingProfile profile = new(
            Ulid.NewUlid(),
            "Scale Down",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                V2RateControlMode.Crf,
                22,
                4000,
                null,
                null,
                null,
                CodecProfile.High,
                null,
                null,
                8,
                null,
                2,
                false,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [],
            []
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.Scale);
    }
}
