using NoMercy.Database;
using NoMercy.Encoder.Dto;
using NoMercy.EncoderV2.Streams;

namespace NoMercy.Tests.EncoderV2.Streams;

public class StreamProcessorTests
{
    #region VideoStreamProcessor - BuildVideoFilterChain Tests

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_NoScalingNeeded_ReturnsEmptyList()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1920, height: 1080, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Empty(filters);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_ScalingNeeded_ReturnsScaleFilter()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1280, height: 720, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Single(filters);
        Assert.Equal("scale=1280:720", filters[0]);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_WidthChangeOnly_ReturnsScaleFilter()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1280, height: 1080, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Single(filters);
        Assert.Contains("scale=1280:1080", filters);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_HeightChangeOnly_ReturnsScaleFilter()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1920, height: 720, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Single(filters);
        Assert.Contains("scale=1920:720", filters);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_HdrToSdrConversion_ReturnsToneMapFilters()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1920, height: 1080, convertHdrToSdr: true);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: true);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.NotEmpty(filters);
        Assert.Contains(filters, f => f.Contains("zscale"));
        Assert.Contains(filters, f => f.Contains("tonemap"));
        Assert.Contains(filters, f => f.Contains("format=yuv420p"));
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_HdrToSdrNotRequested_NoToneMapFilters()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1920, height: 1080, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: true);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Empty(filters);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_SdrWithConversionRequested_NoToneMapFilters()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1920, height: 1080, convertHdrToSdr: true);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Empty(filters);
    }

    [Fact]
    public void VideoStreamProcessor_BuildVideoFilterChain_ScalingAndHdrConversion_ReturnsBothFilters()
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: 1280, height: 720, convertHdrToSdr: true);
        VideoStream videoStream = CreateVideoStream(width: 1920, height: 1080, isHdr: true);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Contains(filters, f => f.Contains("scale=1280:720"));
        Assert.Contains(filters, f => f.Contains("tonemap"));
    }

    [Theory]
    [InlineData(3840, 2160, 1920, 1080)] // 4K to 1080p
    [InlineData(1920, 1080, 1280, 720)]  // 1080p to 720p
    [InlineData(1920, 1080, 854, 480)]   // 1080p to 480p
    [InlineData(1280, 720, 640, 360)]    // 720p to 360p
    public void VideoStreamProcessor_BuildVideoFilterChain_CommonDownscales_ReturnsCorrectScale(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        VideoStreamProcessor processor = new();
        IVideoProfile profile = CreateVideoProfile(width: targetWidth, height: targetHeight, convertHdrToSdr: false);
        VideoStream videoStream = CreateVideoStream(width: sourceWidth, height: sourceHeight, isHdr: false);

        List<string> filters = processor.BuildVideoFilterChain(profile, videoStream);

        Assert.Single(filters);
        Assert.Equal($"scale={targetWidth}:{targetHeight}", filters[0]);
    }

    #endregion

    #region AudioStreamProcessor - BuildAudioFilterChain Tests

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_NoChangesNeeded_ReturnsEmptyList()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 48000, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 2);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Empty(filters);
    }

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_SampleRateChange_ReturnsResampleFilter()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 44100, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 2);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Single(filters);
        Assert.Equal("aresample=44100", filters[0]);
    }

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_ChannelDownmix_ReturnsPanFilter()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 48000, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 6);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Single(filters);
        Assert.Contains("pan=stereo", filters[0]);
    }

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_BothChanges_ReturnsBothFilters()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 44100, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 6);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Equal(2, filters.Count);
        Assert.Contains(filters, f => f.Contains("aresample=44100"));
        Assert.Contains(filters, f => f.Contains("pan=stereo"));
    }

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_ZeroTargetSampleRate_NoResampleFilter()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 0, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 2);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Empty(filters);
    }

    [Fact]
    public void AudioStreamProcessor_BuildAudioFilterChain_ZeroTargetChannels_NoPanFilter()
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: 48000, channels: 0);
        AudioStream audioStream = CreateAudioStream(sampleRate: 48000, channels: 6);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Empty(filters);
    }

    [Theory]
    [InlineData(48000, 44100)]
    [InlineData(96000, 48000)]
    [InlineData(44100, 22050)]
    public void AudioStreamProcessor_BuildAudioFilterChain_CommonSampleRates_ReturnsCorrectResample(
        int sourceSampleRate, int targetSampleRate)
    {
        AudioStreamProcessor processor = new();
        IAudioProfile profile = CreateAudioProfile(sampleRate: targetSampleRate, channels: 2);
        AudioStream audioStream = CreateAudioStream(sampleRate: sourceSampleRate, channels: 2);

        List<string> filters = processor.BuildAudioFilterChain(profile, audioStream);

        Assert.Single(filters);
        Assert.Equal($"aresample={targetSampleRate}", filters[0]);
    }

    #endregion

    #region SubtitleStreamProcessor - RequiresConversion Tests

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_SameCodec_ReturnsFalse()
    {
        SubtitleStreamProcessor processor = new();

        bool result = processor.RequiresConversion("srt", "srt");

        Assert.False(result);
    }

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_DifferentCodecs_ReturnsTrue()
    {
        SubtitleStreamProcessor processor = new();

        bool result = processor.RequiresConversion("srt", "webvtt");

        Assert.True(result);
    }

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_SubripToSrt_ReturnsFalse()
    {
        SubtitleStreamProcessor processor = new();

        // subrip is the internal codec name for srt
        bool result = processor.RequiresConversion("subrip", "srt");

        Assert.False(result);
    }

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_CaseInsensitive_ReturnsFalse()
    {
        SubtitleStreamProcessor processor = new();

        bool result = processor.RequiresConversion("ASS", "ass");

        Assert.False(result);
    }

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_AssToWebvtt_ReturnsTrue()
    {
        SubtitleStreamProcessor processor = new();

        bool result = processor.RequiresConversion("ass", "webvtt");

        Assert.True(result);
    }

    [Fact]
    public void SubtitleStreamProcessor_RequiresConversion_SsaToAss_ReturnsTrue()
    {
        SubtitleStreamProcessor processor = new();

        // SSA and ASS are different formats
        bool result = processor.RequiresConversion("ssa", "ass");

        Assert.True(result);
    }

    [Theory]
    [InlineData("srt", "webvtt", true)]
    [InlineData("ass", "srt", true)]
    [InlineData("mov_text", "srt", true)]
    [InlineData("webvtt", "webvtt", false)]
    [InlineData("ass", "ass", false)]
    public void SubtitleStreamProcessor_RequiresConversion_CommonConversions(
        string inputCodec, string outputCodec, bool expectedResult)
    {
        SubtitleStreamProcessor processor = new();

        bool result = processor.RequiresConversion(inputCodec, outputCodec);

        Assert.Equal(expectedResult, result);
    }

    #endregion

    #region Helper Methods

    private static IVideoProfile CreateVideoProfile(int width, int height, bool convertHdrToSdr)
    {
        return new IVideoProfile
        {
            Codec = "h264",
            Width = width,
            Height = height,
            ConvertHdrToSdr = convertHdrToSdr,
            Bitrate = 5000000,
            Framerate = 30,
            Preset = "medium",
            Profile = "high",
            Crf = 23
        };
    }

    private static VideoStream CreateVideoStream(int width, int height, bool isHdr)
    {
        return new VideoStream(new FfprobeSourceDataStream
        {
            Index = 0,
            Width = width,
            Height = height,
            CodecName = "h264",
            CodecType = CodecType.Video,
            AvgFrameRate = "30/1",
            ColorTransfer = isHdr ? "smpte2084" : "bt709",
            ColorPrimaries = isHdr ? "bt2020" : "bt709",
            PixFmt = isHdr ? "yuv420p10le" : "yuv420p",
            Disposition = new Dictionary<string, int> { { "default", 1 } },
            Tags = new Dictionary<string, string>()
        });
    }

    private static IAudioProfile CreateAudioProfile(int sampleRate, int channels)
    {
        return new IAudioProfile
        {
            Codec = "aac",
            SampleRate = sampleRate,
            Channels = channels,
            AllowedLanguages = ["eng", "jpn"]
        };
    }

    private static AudioStream CreateAudioStream(int sampleRate, int channels)
    {
        return new AudioStream(new FfprobeSourceDataStream
        {
            Index = 1,
            CodecName = "aac",
            CodecType = CodecType.Audio,
            SampleRate = sampleRate,
            Channels = channels,
            ChannelLayout = channels == 2 ? "stereo" : "5.1",
            Disposition = new Dictionary<string, int> { { "default", 1 } },
            Tags = new Dictionary<string, string> { { "language", "eng" } }
        });
    }

    #endregion
}
