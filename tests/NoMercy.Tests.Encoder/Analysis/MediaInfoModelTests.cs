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

namespace NoMercy.Tests.Encoder.Analysis;

public class MediaInfoModelTests
{
    [Fact]
    public void VideoStreamInfo_HdrDetection_Smpte2084_IsTrueHdr()
    {
        VideoStreamInfo stream = new(
            0,
            "hevc",
            3840,
            2160,
            23.976,
            10,
            "yuv420p10le",
            "bt2020",
            "smpte2084",
            "bt2020nc",
            true,
            40000
        );
        stream.IsHdr.Should().BeTrue();
    }

    [Fact]
    public void VideoStreamInfo_HdrDetection_Bt709_IsNotHdr()
    {
        VideoStreamInfo stream = new(
            0,
            "h264",
            1920,
            1080,
            24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            8000
        );
        stream.IsHdr.Should().BeFalse();
    }

    [Fact]
    public void VideoStreamInfo_HdrDetection_Hlg_IsTrueHdr()
    {
        VideoStreamInfo stream = new(
            0,
            "hevc",
            3840,
            2160,
            50.0,
            10,
            "yuv420p10le",
            "bt2020",
            "arib-std-b67",
            "bt2020nc",
            true,
            30000
        );
        stream.IsHdr.Should().BeTrue();
    }

    [Fact]
    public void SubtitleStreamInfo_TextType_ClassifiedCorrectly()
    {
        SubtitleStreamInfo srt = new(
            0,
            "srt",
            "eng",
            true,
            false
        );
        SubtitleStreamInfo ass = new(
            1,
            "ass",
            "jpn",
            false,
            false
        );
        SubtitleStreamInfo vtt = new(
            2,
            "webvtt",
            "eng",
            false,
            false
        );
        srt.IsTextBased.Should().BeTrue();
        ass.IsTextBased.Should().BeTrue();
        vtt.IsTextBased.Should().BeTrue();
    }

    [Fact]
    public void SubtitleStreamInfo_BitmapType_ClassifiedCorrectly()
    {
        SubtitleStreamInfo pgs = new(
            0,
            "hdmv_pgs_subtitle",
            "eng",
            true,
            false
        );
        SubtitleStreamInfo dvd = new(
            1,
            "dvd_subtitle",
            "eng",
            false,
            false
        );
        pgs.IsTextBased.Should().BeFalse();
        dvd.IsTextBased.Should().BeFalse();
    }

    [Fact]
    public void MediaInfo_WithAllStreamTypes_StoresCorrectly()
    {
        List<VideoStreamInfo> videoStreams = [CreateVideoStream()];
        List<AudioStreamInfo> audioStreams = [CreateAudioStream()];
        List<SubtitleStreamInfo> subtitleStreams = [CreateSubtitleStream()];
        List<ChapterInfo> chapters =
        [
            new(TimeSpan.Zero, TimeSpan.FromMinutes(5), "Intro"),
        ];

        MediaInfo info = new(
            "/media/movie.mkv",
            "matroska",
            TimeSpan.FromHours(2),
            20000,
            18_000_000_000L,
            videoStreams,
            audioStreams,
            subtitleStreams,
            chapters
        );

        info.VideoStreams.Should().HaveCount(1);
        info.AudioStreams.Should().HaveCount(1);
        info.SubtitleStreams.Should().HaveCount(1);
        info.Chapters.Should().HaveCount(1);
        info.HasVideo.Should().BeTrue();
        info.HasAudio.Should().BeTrue();
        info.HasSubtitles.Should().BeTrue();
    }

    [Fact]
    public void MediaInfo_AudioOnly_HasVideoIsFalse()
    {
        MediaInfo info = new(
            "/media/song.flac",
            "flac",
            TimeSpan.FromMinutes(4),
            1411,
            42_000_000L,
            [],
            [CreateAudioStream()],
            [],
            []
        );
        info.HasVideo.Should().BeFalse();
        info.HasAudio.Should().BeTrue();
        info.HasSubtitles.Should().BeFalse();
    }

    [Fact]
    public void MediaInfo_IsVariableFrameRate_DetectedFromMismatch()
    {
        VideoStreamInfo vfrStream = new(
            0,
            "h264",
            1920,
            1080,
            29.97,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            5000,
            24.5,
            30.0
        );
        vfrStream.IsVariableFrameRate.Should().BeTrue();
    }

    private static VideoStreamInfo CreateVideoStream() =>
        new(
            0,
            "h264",
            1920,
            1080,
            24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            8000
        );

    private static AudioStreamInfo CreateAudioStream() =>
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

    private static SubtitleStreamInfo CreateSubtitleStream() =>
        new(2, "srt", "eng", true, false);
}
