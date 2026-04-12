namespace NoMercy.Tests.Encoder.V3.Analysis;

using NoMercy.Encoder.V3.Analysis;

public class MediaInfoModelTests
{
    [Fact]
    public void VideoStreamInfo_HdrDetection_Smpte2084_IsTrueHdr()
    {
        VideoStreamInfo stream = new(
            Index: 0,
            Codec: "hevc",
            Width: 3840,
            Height: 2160,
            FrameRate: 23.976,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: "bt2020",
            ColorTransfer: "smpte2084",
            ColorSpace: "bt2020nc",
            IsDefault: true,
            BitRateKbps: 40000
        );
        stream.IsHdr.Should().BeTrue();
    }

    [Fact]
    public void VideoStreamInfo_HdrDetection_Bt709_IsNotHdr()
    {
        VideoStreamInfo stream = new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );
        stream.IsHdr.Should().BeFalse();
    }

    [Fact]
    public void VideoStreamInfo_HdrDetection_Hlg_IsTrueHdr()
    {
        VideoStreamInfo stream = new(
            Index: 0,
            Codec: "hevc",
            Width: 3840,
            Height: 2160,
            FrameRate: 50.0,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: "bt2020",
            ColorTransfer: "arib-std-b67",
            ColorSpace: "bt2020nc",
            IsDefault: true,
            BitRateKbps: 30000
        );
        stream.IsHdr.Should().BeTrue();
    }

    [Fact]
    public void SubtitleStreamInfo_TextType_ClassifiedCorrectly()
    {
        SubtitleStreamInfo srt = new(
            Index: 0,
            Codec: "srt",
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );
        SubtitleStreamInfo ass = new(
            Index: 1,
            Codec: "ass",
            Language: "jpn",
            IsDefault: false,
            IsForced: false
        );
        SubtitleStreamInfo vtt = new(
            Index: 2,
            Codec: "webvtt",
            Language: "eng",
            IsDefault: false,
            IsForced: false
        );
        srt.IsTextBased.Should().BeTrue();
        ass.IsTextBased.Should().BeTrue();
        vtt.IsTextBased.Should().BeTrue();
    }

    [Fact]
    public void SubtitleStreamInfo_BitmapType_ClassifiedCorrectly()
    {
        SubtitleStreamInfo pgs = new(
            Index: 0,
            Codec: "hdmv_pgs_subtitle",
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );
        SubtitleStreamInfo dvd = new(
            Index: 1,
            Codec: "dvd_subtitle",
            Language: "eng",
            IsDefault: false,
            IsForced: false
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
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(5), Title: "Intro"),
        ];

        MediaInfo info = new(
            FilePath: "/media/movie.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromHours(2),
            OverallBitRateKbps: 20000,
            FileSizeBytes: 18_000_000_000L,
            VideoStreams: videoStreams,
            AudioStreams: audioStreams,
            SubtitleStreams: subtitleStreams,
            Chapters: chapters
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
            FilePath: "/media/song.flac",
            Format: "flac",
            Duration: TimeSpan.FromMinutes(4),
            OverallBitRateKbps: 1411,
            FileSizeBytes: 42_000_000L,
            VideoStreams: [],
            AudioStreams: [CreateAudioStream()],
            SubtitleStreams: [],
            Chapters: []
        );
        info.HasVideo.Should().BeFalse();
        info.HasAudio.Should().BeTrue();
        info.HasSubtitles.Should().BeFalse();
    }

    [Fact]
    public void MediaInfo_IsVariableFrameRate_DetectedFromMismatch()
    {
        VideoStreamInfo vfrStream = new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 29.97,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 5000,
            AverageFrameRate: 24.5,
            RealFrameRate: 30.0
        );
        vfrStream.IsVariableFrameRate.Should().BeTrue();
    }

    private static VideoStreamInfo CreateVideoStream() =>
        new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );

    private static AudioStreamInfo CreateAudioStream() =>
        new(
            Index: 1,
            Codec: "aac",
            Channels: 2,
            SampleRate: 48000,
            BitRateKbps: 192,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );

    private static SubtitleStreamInfo CreateSubtitleStream() =>
        new(Index: 2, Codec: "srt", Language: "eng", IsDefault: true, IsForced: false);
}
