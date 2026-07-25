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
using NoMercy.Encoder.Pipeline.Optimizer;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class ExecutionGraphBuilderThumbnailTests
{
    [Fact]
    public void Thumbnail_HdrSource_DependsOnTonemapNode()
    {
        MediaInfo media = BuildMedia("smpte2084", 3840, 2160);
        EncodingProfile profile = BuildProfileWithThumbnails();

        List<ExecutionNode> nodes = new ExecutionGraphBuilder().BuildGraph(
            media,
            profile,
            ResolveSingle()
        );

        ExecutionNode tonemap = nodes.Single(n => n.Operation == OperationType.Tonemap);
        ExecutionNode thumb = nodes.Single(n => n.Operation == OperationType.ThumbnailCapture);

        thumb
            .DependsOn.Should()
            .Contain(tonemap.Id, "HDR sprites derive from the SDR intermediate");
    }

    [Fact]
    public void Thumbnail_SdrSource_HasNoVideoDependency()
    {
        MediaInfo media = BuildMedia("bt709", 1920, 1080);
        EncodingProfile profile = BuildProfileWithThumbnails();

        List<ExecutionNode> nodes = new ExecutionGraphBuilder().BuildGraph(
            media,
            profile,
            ResolveSingle()
        );

        nodes.Should().NotContain(n => n.Operation == OperationType.Tonemap);
        ExecutionNode thumb = nodes.Single(n => n.Operation == OperationType.ThumbnailCapture);
        thumb.DependsOn.Should().BeEmpty("SDR sprites read the decoded source directly");
    }

    private static MediaInfo BuildMedia(string transfer, int width, int height) =>
        new(
            "/media/test.mkv",
            "matroska",
            TimeSpan.FromMinutes(90),
            50000,
            30_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    width,
                    height,
                    24.0,
                    transfer == "smpte2084" ? 10 : 8,
                    transfer == "smpte2084" ? "yuv420p10le" : "yuv420p",
                    transfer == "smpte2084" ? "bt2020" : "bt709",
                    transfer,
                    transfer == "smpte2084" ? "bt2020nc" : "bt709",
                    true,
                    45000
                ),
            ],
            [],
            [],
            []
        );

    private static EncodingProfile BuildProfileWithThumbnails() =>
        new(
            Ulid.NewUlid(),
            "Thumb Test",
            Container.HlsTs,
            new(
                StreamPolicy.Transcode,
                VideoCodecType.H265,
                1920,
                1080,
                V2RateControlMode.Crf,
                22,
                5000,
                null,
                null,
                "medium",
                CodecProfile.Main,
                "5.1",
                null,
                8,
                null,
                2,
                true,
                "video/{label}",
                "video/{label}/playlist"
            ),
            [],
            [],
            new(320, 10)
        );

    private static ResolvedCodec[] ResolveSingle() =>
        [
            new(
                "libx265",
                new(
                    "libx265",
                    null,
                    ["medium"],
                    ["main"],
                    ["5.1"],
                    new(0, 51, 28),
                    [RateControlMode.Crf],
                    true,
                    true,
                    int.MaxValue,
                    "yuv420p10le",
                    new()
                ),
                null,
                RateControlMode.Crf
            ),
        ];
}
