using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitleOutput = NoMercy.Encoder.Profiles.SubtitleOutput;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Pipeline;

public class StreamActionResolverTests
{
    private readonly StreamActionResolver _resolver = new();

    // --- Audio ---

    [Fact]
    public void Audio_MatchingCodecAndSufficientBitrate_Copy()
    {
        AudioStreamInfo source = new(0, "aac", 2, 48000, 192, "eng", true, false);
        AudioOutput profile = new(
            StreamPolicy.Transcode,
            AudioCodecType.Aac,
            128,
            2,
            48000,
            [],
            null,
            null,
            null,
            ":type:_:language:_:codec:/:type:_:language:_:codec:",
            ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver.ResolveAudio(source, profile, OutputFormat.Mkv).Should().Be(StreamAction.Copy);
    }

    [Fact]
    public void Audio_DifferentCodec_Transcode()
    {
        AudioStreamInfo source = new(0, "ac3", 6, 48000, 640, "eng", true, false);
        AudioOutput profile = new(
            StreamPolicy.Transcode,
            AudioCodecType.Aac,
            192,
            2,
            48000,
            [],
            null,
            null,
            null,
            ":type:_:language:_:codec:/:type:_:language:_:codec:",
            ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source, profile, OutputFormat.Mkv)
            .Should()
            .Be(StreamAction.Transcode);
    }

    [Fact]
    public void Audio_LosslessSourceLossyTarget_AlwaysTranscode()
    {
        AudioStreamInfo source = new(0, "flac", 2, 48000, 900, "eng", true, false);
        AudioOutput profile = new(
            StreamPolicy.Transcode,
            AudioCodecType.Aac,
            192,
            2,
            48000,
            [],
            null,
            null,
            null,
            ":type:_:language:_:codec:/:type:_:language:_:codec:",
            ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source, profile, OutputFormat.Mkv)
            .Should()
            .Be(StreamAction.Transcode);
    }

    [Fact]
    public void Audio_InsufficientChannels_Transcode()
    {
        AudioStreamInfo source = new(0, "aac", 2, 48000, 192, "eng", true, false);
        AudioOutput profile = new(
            StreamPolicy.Transcode,
            AudioCodecType.Aac,
            192,
            6,
            48000,
            [],
            null,
            null,
            null,
            ":type:_:language:_:codec:/:type:_:language:_:codec:",
            ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source, profile, OutputFormat.Mkv)
            .Should()
            .Be(StreamAction.Transcode);
    }

    // --- Subtitle: text ---

    [Fact]
    public void Subtitle_TextSub_Mkv_Copy()
    {
        SubtitleStreamInfo source = new(0, "srt", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.Srt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver.ResolveSubtitle(source, profile, OutputFormat.Mkv).Should().Be(StreamAction.Copy);
    }

    [Fact]
    public void Subtitle_TextSub_Hls_Extract()
    {
        SubtitleStreamInfo source = new(0, "srt", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source, profile, OutputFormat.Hls)
            .Should()
            .Be(StreamAction.Extract);
    }

    [Fact]
    public void Subtitle_TextSub_Mp4_Extract()
    {
        SubtitleStreamInfo source = new(0, "ass", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source, profile, OutputFormat.Mp4)
            .Should()
            .Be(StreamAction.Extract);
    }

    // --- Subtitle: bitmap ---

    [Fact]
    public void Subtitle_BitmapSub_Mkv_Copy()
    {
        SubtitleStreamInfo source = new(0, "hdmv_pgs_subtitle", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver.ResolveSubtitle(source, profile, OutputFormat.Mkv).Should().Be(StreamAction.Copy);
    }

    [Fact]
    public void Subtitle_BitmapSub_Hls_Transcode()
    {
        // Bitmap subs for HLS must be burned in (mapped to Transcode)
        SubtitleStreamInfo source = new(0, "hdmv_pgs_subtitle", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.Extract,
            SubtitleCodecType.WebVtt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source, profile, OutputFormat.Hls)
            .Should()
            .Be(StreamAction.Transcode);
    }

    [Fact]
    public void Subtitle_BurnInMode_AlwaysTranscode()
    {
        SubtitleStreamInfo source = new(0, "srt", "eng", true, false);
        SubtitleOutput profile = new(
            SubtitlePolicy.BurnIn,
            SubtitleCodecType.WebVtt,
            [],
            false,
            null,
            "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source, profile, OutputFormat.Mkv)
            .Should()
            .Be(StreamAction.Transcode);
    }

    // --- Video ---

    [Fact]
    public void Video_DifferentCodec_Transcode()
    {
        VideoStreamInfo source = new(
            0,
            "hevc",
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
        VideoOutput profile = new(
            StreamPolicy.Transcode,
            VideoCodecType.H264,
            1920,
            1080,
            RateControlMode.Crf,
            0,
            8000,
            null,
            null,
            null,
            CodecProfile.Auto,
            null,
            null,
            8,
            null,
            0,
            false,
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source, profile).Should().Be(StreamAction.Transcode);
    }

    [Fact]
    public void Video_SameCodecSameRes_Copy()
    {
        VideoStreamInfo source = new(
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
        VideoOutput profile = new(
            StreamPolicy.Transcode,
            VideoCodecType.H264,
            1920,
            1080,
            RateControlMode.Crf,
            0,
            8000,
            null,
            null,
            null,
            CodecProfile.Auto,
            null,
            null,
            8,
            null,
            0,
            false,
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source, profile).Should().Be(StreamAction.Copy);
    }

    [Fact]
    public void Video_SameCodecDifferentRes_Transcode()
    {
        VideoStreamInfo source = new(
            0,
            "h264",
            3840,
            2160,
            24.0,
            8,
            "yuv420p",
            "bt709",
            "bt709",
            "bt709",
            true,
            20000
        );
        VideoOutput profile = new(
            StreamPolicy.Transcode,
            VideoCodecType.H264,
            1920,
            1080,
            RateControlMode.Crf,
            0,
            8000,
            null,
            null,
            null,
            CodecProfile.Auto,
            null,
            null,
            8,
            null,
            0,
            false,
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source, profile).Should().Be(StreamAction.Transcode);
    }
}
