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

namespace NoMercy.Tests.Encoder.Codecs;

public class CodecRegistryTests
{
    private readonly CodecRegistry _registry = new();

    [Theory]
    [InlineData(data: VideoCodecType.H264)]
    [InlineData(data: VideoCodecType.H265)]
    [InlineData(data: VideoCodecType.Av1)]
    [InlineData(data: VideoCodecType.Vp9)]
    public void GetDefinition_ReturnsForAllVideoCodecs(VideoCodecType codecType)
    {
        ICodecDefinition definition = _registry.GetVideoDefinition(codecType: codecType);
        definition.Should().NotBeNull();
        definition.CodecType.Should().Be(expected: codecType);
        definition.Encoders.Should().NotBeEmpty();
    }

    [Fact]
    public void GetVideoEncoder_ByFfmpegName_ReturnsCorrect()
    {
        EncoderInfo? nvenc = _registry.GetVideoEncoderByName(ffmpegName: "h264_nvenc");
        nvenc.Should().NotBeNull();
        nvenc!.FfmpegName.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void GetVideoEncoder_UnknownName_ReturnsNull()
    {
        EncoderInfo? unknown = _registry.GetVideoEncoderByName(ffmpegName: "vp9_nvenc");
        unknown.Should().BeNull();
    }

    [Fact]
    public void AllVideoEncoders_HaveUniqueNames()
    {
        List<string> allNames = [];
        foreach (VideoCodecType codecType in Enum.GetValues<VideoCodecType>())
        {
            ICodecDefinition def = _registry.GetVideoDefinition(codecType: codecType);
            allNames.AddRange(collection: def.Encoders.Select(selector: e => e.FfmpegName));
        }
        allNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TotalVideoEncoderCount_Is23()
    {
        // 22 real encoders (H.264 × 6 + H.265 × 6 + AV1 × 5 + VP9 × 5)
        // plus 1 synthetic "copy" encoder from CopyVideoDefinition. Bumped
        // from 22 when stream-copy passthrough landed.
        int total = 0;
        foreach (VideoCodecType codecType in Enum.GetValues<VideoCodecType>())
        {
            total += _registry.GetVideoDefinition(codecType: codecType).Encoders.Length;
        }
        total.Should().Be(expected: 23);
    }
}
