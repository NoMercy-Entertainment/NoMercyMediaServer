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

using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Analysis;

public class MediaAnalyzerTests
{
    private static string LoadFixture(string name)
    {
        string path = Path.Combine(path1: AppContext.BaseDirectory, path2: "Analysis", path3: "Fixtures", path4: name);
        return File.ReadAllText(path: path);
    }

    private static MediaInfo Parse(string fixtureName, string filePath = "/media/test.mkv")
    {
        string json = LoadFixture(name: fixtureName);
        return MediaAnalyzer.ParseFfprobeJson(json: json, filePath: filePath);
    }

    [Fact]
    public void Parse_1080p_H264_Aac_CorrectStreamCounts()
    {
        MediaInfo info = Parse(fixtureName: "fixture-1080p-h264-aac.json");

        info.VideoStreams.Should().HaveCount(expected: 1);
        info.AudioStreams.Should().HaveCount(expected: 1);
        info.SubtitleStreams.Should().HaveCount(expected: 0);
        info.Chapters.Should().HaveCount(expected: 0);
        info.HasVideo.Should().BeTrue();
        info.HasAudio.Should().BeTrue();
        info.HasSubtitles.Should().BeFalse();
    }

    [Fact]
    public void Parse_1080p_H264_Aac_VideoFields()
    {
        MediaInfo info = Parse(fixtureName: "fixture-1080p-h264-aac.json");
        VideoStreamInfo video = info.VideoStreams[index: 0];

        video.Index.Should().Be(expected: 0);
        video.Codec.Should().Be(expected: "h264");
        video.Width.Should().Be(expected: 1920);
        video.Height.Should().Be(expected: 1080);
        video.BitDepth.Should().Be(expected: 8);
        video.PixelFormat.Should().Be(expected: "yuv420p");
        video.ColorPrimaries.Should().Be(expected: "bt709");
        video.ColorTransfer.Should().Be(expected: "bt709");
        video.ColorSpace.Should().Be(expected: "bt709");
        video.IsDefault.Should().BeTrue();
        video.IsHdr.Should().BeFalse();
        video.BitRateKbps.Should().Be(expected: 8000);
    }

    [Fact]
    public void Parse_1080p_H264_Aac_FrameRate()
    {
        MediaInfo info = Parse(fixtureName: "fixture-1080p-h264-aac.json");
        VideoStreamInfo video = info.VideoStreams[index: 0];

        video.FrameRate.Should().BeApproximately(expectedValue: 23.976, precision: 0.001);
    }

    [Fact]
    public void Parse_1080p_H264_Aac_FormatFields()
    {
        MediaInfo info = Parse(fixtureName: "fixture-1080p-h264-aac.json");

        info.Format.Should().Be(expected: "matroska,webm");
        info.Duration.Should().Be(expected: TimeSpan.FromSeconds(seconds: 7200));
        info.OverallBitRateKbps.Should().Be(expected: 8192);
        info.FileSizeBytes.Should().Be(expected: 7_372_800_000L);
    }

    [Fact]
    public void Parse_1080p_H264_Aac_AudioFields()
    {
        MediaInfo info = Parse(fixtureName: "fixture-1080p-h264-aac.json");
        AudioStreamInfo audio = info.AudioStreams[index: 0];

        audio.Index.Should().Be(expected: 1);
        audio.Codec.Should().Be(expected: "aac");
        audio.Channels.Should().Be(expected: 2);
        audio.SampleRate.Should().Be(expected: 48000);
        audio.BitRateKbps.Should().Be(expected: 192);
        audio.Language.Should().Be(expected: "eng");
        audio.IsDefault.Should().BeTrue();
        audio.IsForced.Should().BeFalse();
    }

    [Fact]
    public void Parse_4k_Hdr_Hevc_IsHdr()
    {
        MediaInfo info = Parse(fixtureName: "fixture-4k-hdr-hevc-truehd.json");
        VideoStreamInfo video = info.VideoStreams[index: 0];

        video.Codec.Should().Be(expected: "hevc");
        video.Width.Should().Be(expected: 3840);
        video.Height.Should().Be(expected: 2160);
        video.BitDepth.Should().Be(expected: 10);
        video.ColorPrimaries.Should().Be(expected: "bt2020");
        video.ColorTransfer.Should().Be(expected: "smpte2084");
        video.IsHdr.Should().BeTrue();
    }

    [Fact]
    public void Parse_4k_Hdr_Hevc_ChaptersParsed()
    {
        MediaInfo info = Parse(fixtureName: "fixture-4k-hdr-hevc-truehd.json");

        info.Chapters.Should().HaveCount(expected: 3);
        info.Chapters[index: 0].Title.Should().Be(expected: "Chapter 1");
        info.Chapters[index: 0].Start.Should().Be(expected: TimeSpan.Zero);
        info.Chapters[index: 0].End.Should().Be(expected: TimeSpan.FromSeconds(seconds: 1200));
        info.Chapters[index: 1].Title.Should().Be(expected: "Chapter 2");
        info.Chapters[index: 1].Start.Should().Be(expected: TimeSpan.FromSeconds(seconds: 1200));
        info.Chapters[index: 2].Title.Should().Be(expected: "Chapter 3");
        info.Chapters[index: 2].End.Should().Be(expected: TimeSpan.FromSeconds(seconds: 7200));
    }

    [Fact]
    public void Parse_4k_Hdr_Hevc_MultipleAudioStreams()
    {
        MediaInfo info = Parse(fixtureName: "fixture-4k-hdr-hevc-truehd.json");

        info.AudioStreams.Should().HaveCount(expected: 2);
        info.AudioStreams[index: 0].Codec.Should().Be(expected: "truehd");
        info.AudioStreams[index: 0].Channels.Should().Be(expected: 8);
        info.AudioStreams[index: 0].IsDefault.Should().BeTrue();
        info.AudioStreams[index: 1].Codec.Should().Be(expected: "ac3");
        info.AudioStreams[index: 1].Channels.Should().Be(expected: 6);
        info.AudioStreams[index: 1].IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Parse_Dvd_Mpeg2_Ac3_CorrectCodecAndResolution()
    {
        MediaInfo info = Parse(fixtureName: "fixture-dvd-mpeg2-ac3.json");
        VideoStreamInfo video = info.VideoStreams[index: 0];

        video.Codec.Should().Be(expected: "mpeg2video");
        video.Width.Should().Be(expected: 720);
        video.Height.Should().Be(expected: 480);
        video.BitDepth.Should().Be(expected: 8);
        info.Format.Should().Be(expected: "mpeg");
    }

    [Fact]
    public void Parse_Dvd_Mpeg2_Ac3_BitmapSubtitle()
    {
        MediaInfo info = Parse(fixtureName: "fixture-dvd-mpeg2-ac3.json");

        info.SubtitleStreams.Should().HaveCount(expected: 1);
        SubtitleStreamInfo sub = info.SubtitleStreams[index: 0];
        sub.Codec.Should().Be(expected: "dvd_subtitle");
        sub.IsTextBased.Should().BeFalse();
        sub.Language.Should().Be(expected: "eng");
    }

    [Fact]
    public void Parse_MultiAudioSubs_CorrectCounts()
    {
        MediaInfo info = Parse(fixtureName: "fixture-multi-audio-subs.json");

        info.VideoStreams.Should().HaveCount(expected: 1);
        info.AudioStreams.Should().HaveCount(expected: 3);
        info.SubtitleStreams.Should().HaveCount(expected: 5);
    }

    [Fact]
    public void Parse_MultiAudioSubs_ForcedSubtitleDetected()
    {
        MediaInfo info = Parse(fixtureName: "fixture-multi-audio-subs.json");

        SubtitleStreamInfo forced = info.SubtitleStreams.Single(predicate: s => s.IsForced);
        forced.Index.Should().Be(expected: 6);
        forced.Codec.Should().Be(expected: "ass");
        forced.Language.Should().Be(expected: "eng");
    }

    [Fact]
    public void Parse_MultiAudioSubs_TextVsBitmapClassification()
    {
        MediaInfo info = Parse(fixtureName: "fixture-multi-audio-subs.json");

        IReadOnlyList<SubtitleStreamInfo> subs = info.SubtitleStreams;

        subs[index: 0].Codec.Should().Be(expected: "ass");
        subs[index: 0].IsTextBased.Should().BeTrue();

        subs[index: 1].Codec.Should().Be(expected: "ass");
        subs[index: 1].IsTextBased.Should().BeTrue();

        subs[index: 2].Codec.Should().Be(expected: "ass");
        subs[index: 2].IsTextBased.Should().BeTrue();

        subs[index: 3].Codec.Should().Be(expected: "hdmv_pgs_subtitle");
        subs[index: 3].IsTextBased.Should().BeFalse();

        subs[index: 4].Codec.Should().Be(expected: "subrip");
        subs[index: 4].IsTextBased.Should().BeTrue();
    }

    [Fact]
    public void Parse_AudioCd_Flac_HasVideoFalse()
    {
        MediaInfo info = Parse(fixtureName: "fixture-audio-cd-flac.json");

        info.HasVideo.Should().BeFalse();
        info.HasAudio.Should().BeTrue();
        info.VideoStreams.Should().HaveCount(expected: 0);
    }

    [Fact]
    public void Parse_AudioCd_Flac_AudioFields()
    {
        MediaInfo info = Parse(fixtureName: "fixture-audio-cd-flac.json");
        AudioStreamInfo audio = info.AudioStreams[index: 0];

        audio.Codec.Should().Be(expected: "flac");
        audio.Channels.Should().Be(expected: 2);
        audio.SampleRate.Should().Be(expected: 44100);
    }

    [Fact]
    public void Parse_AudioCd_Flac_ChaptersParsed()
    {
        MediaInfo info = Parse(fixtureName: "fixture-audio-cd-flac.json");

        info.Chapters.Should().HaveCount(expected: 3);
        info.Chapters[index: 0].Title.Should().Be(expected: "Track 01");
        info.Chapters[index: 0].Start.Should().Be(expected: TimeSpan.Zero);
        info.Chapters[index: 0].End.Should().Be(expected: TimeSpan.FromSeconds(seconds: 240));
        info.Chapters[index: 1].Title.Should().Be(expected: "Track 02");
        info.Chapters[index: 2].Title.Should().Be(expected: "Track 03");
        info.Chapters[index: 2].End.Should().Be(expected: TimeSpan.FromSeconds(seconds: 780));
    }

    [Fact]
    public async Task AnalyzeAsync_UsesProcessRunner_AndParsesResult()
    {
        string json = LoadFixture(name: "fixture-1080p-h264-aac.json");
        Mock<IProcessRunner> mockRunner = new();
        mockRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: json, StdErr: "", Duration: TimeSpan.FromMilliseconds(milliseconds: 50)));

        MediaAnalyzer analyzer = new(
            processRunner: mockRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            options: new() { FfprobePathOverride = "ffprobe" }
        );
        MediaInfo info = await analyzer.AnalyzeAsync(filePath: "/media/test.mkv");

        info.VideoStreams.Should().HaveCount(expected: 1);
        info.AudioStreams.Should().HaveCount(expected: 1);
        info.Format.Should().Be(expected: "matroska,webm");

        mockRunner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffprobe",
                    It.Is<string[]>(args =>
                        args[args.Length - 1].EndsWith("test.mkv")
                        && args.Contains("-show_streams")
                        && args.Contains("-show_format")
                        && args.Contains("-show_chapters")
                    ),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task AnalyzeAsync_WhenFfprobeFails_Throws()
    {
        Mock<IProcessRunner> mockRunner = new();
        mockRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "No such file or directory", Duration: TimeSpan.FromMilliseconds(milliseconds: 10))
            );

        MediaAnalyzer analyzer = new(
            processRunner: mockRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            options: new() { FfprobePathOverride = "ffprobe" }
        );

        Func<Task> act = async () => await analyzer.AnalyzeAsync(filePath: "/nonexistent.mkv");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*ffprobe failed*");
    }
}
