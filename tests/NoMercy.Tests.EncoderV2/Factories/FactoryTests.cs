using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Subtitle;
using NoMercy.EncoderV2.Codecs.Video;
using NoMercy.EncoderV2.Containers;
using NoMercy.EncoderV2.Factories;

namespace NoMercy.Tests.EncoderV2.Factories;

public class FactoryTests
{
    #region CodecFactory Video Tests

    [Theory]
    [InlineData("libx264")]
    [InlineData("h264")]
    [InlineData("x264")]
    public void CodecFactory_CreateVideoCodec_H264_ReturnsH264Codec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<H264Codec>(codec);
    }

    [Theory]
    [InlineData("h264_nvenc")]
    public void CodecFactory_CreateVideoCodec_H264Nvenc_ReturnsH264NvencCodec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<H264NvencCodec>(codec);
    }

    [Theory]
    [InlineData("libx265")]
    [InlineData("h265")]
    [InlineData("hevc")]
    public void CodecFactory_CreateVideoCodec_H265_ReturnsH265Codec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<H265Codec>(codec);
    }

    [Theory]
    [InlineData("hevc_nvenc")]
    [InlineData("h265_nvenc")]
    public void CodecFactory_CreateVideoCodec_H265Nvenc_ReturnsH265NvencCodec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<H265NvencCodec>(codec);
    }

    [Theory]
    [InlineData("libaom-av1")]
    [InlineData("av1")]
    public void CodecFactory_CreateVideoCodec_Av1_ReturnsAv1Codec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<Av1Codec>(codec);
    }

    [Theory]
    [InlineData("libsvtav1")]
    [InlineData("svtav1")]
    public void CodecFactory_CreateVideoCodec_Av1Svt_ReturnsAv1SvtCodec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<Av1SvtCodec>(codec);
    }

    [Theory]
    [InlineData("libvpx-vp9")]
    [InlineData("vp9")]
    public void CodecFactory_CreateVideoCodec_Vp9_ReturnsVp9Codec(string name)
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<Vp9Codec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateVideoCodec_Copy_ReturnsVideoCopyCodec()
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec("copy");

        Assert.NotNull(codec);
        Assert.IsType<VideoCopyCodec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateVideoCodec_Unknown_ReturnsNull()
    {
        CodecFactory factory = new();

        IVideoCodec? codec = factory.CreateVideoCodec("unknown_codec");

        Assert.Null(codec);
    }

    [Fact]
    public void CodecFactory_AvailableVideoCodecs_ContainsCommonCodecs()
    {
        CodecFactory factory = new();

        Assert.Contains("libx264", factory.AvailableVideoCodecs);
        Assert.Contains("libx265", factory.AvailableVideoCodecs);
        Assert.Contains("libaom-av1", factory.AvailableVideoCodecs);
        Assert.Contains("libvpx-vp9", factory.AvailableVideoCodecs);
    }

    #endregion

    #region CodecFactory Audio Tests

    [Theory]
    [InlineData("aac")]
    public void CodecFactory_CreateAudioCodec_Aac_ReturnsAacCodec(string name)
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<AacCodec>(codec);
    }

    [Theory]
    [InlineData("libfdk_aac")]
    [InlineData("fdkaac")]
    public void CodecFactory_CreateAudioCodec_FdkAac_ReturnsFdkAacCodec(string name)
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<FdkAacCodec>(codec);
    }

    [Theory]
    [InlineData("libopus")]
    [InlineData("opus")]
    public void CodecFactory_CreateAudioCodec_Opus_ReturnsOpusCodec(string name)
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<OpusCodec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateAudioCodec_Ac3_ReturnsAc3Codec()
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec("ac3");

        Assert.NotNull(codec);
        Assert.IsType<Ac3Codec>(codec);
    }

    [Theory]
    [InlineData("eac3")]
    [InlineData("e-ac3")]
    public void CodecFactory_CreateAudioCodec_Eac3_ReturnsEac3Codec(string name)
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<Eac3Codec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateAudioCodec_Flac_ReturnsFlacCodec()
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec("flac");

        Assert.NotNull(codec);
        Assert.IsType<FlacCodec>(codec);
    }

    [Theory]
    [InlineData("libmp3lame")]
    [InlineData("mp3")]
    public void CodecFactory_CreateAudioCodec_Mp3_ReturnsMp3Codec(string name)
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<Mp3Codec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateAudioCodec_Copy_ReturnsAudioCopyCodec()
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec("copy");

        Assert.NotNull(codec);
        Assert.IsType<AudioCopyCodec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateAudioCodec_Unknown_ReturnsNull()
    {
        CodecFactory factory = new();

        IAudioCodec? codec = factory.CreateAudioCodec("unknown_codec");

        Assert.Null(codec);
    }

    #endregion

    #region CodecFactory Subtitle Tests

    [Theory]
    [InlineData("webvtt")]
    [InlineData("vtt")]
    public void CodecFactory_CreateSubtitleCodec_Webvtt_ReturnsWebvttCodec(string name)
    {
        CodecFactory factory = new();

        ISubtitleCodec? codec = factory.CreateSubtitleCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<WebvttCodec>(codec);
    }

    [Theory]
    [InlineData("srt")]
    [InlineData("subrip")]
    public void CodecFactory_CreateSubtitleCodec_Srt_ReturnsSrtCodec(string name)
    {
        CodecFactory factory = new();

        ISubtitleCodec? codec = factory.CreateSubtitleCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<SrtCodec>(codec);
    }

    [Theory]
    [InlineData("ass")]
    [InlineData("ssa")]
    public void CodecFactory_CreateSubtitleCodec_Ass_ReturnsAssCodec(string name)
    {
        CodecFactory factory = new();

        ISubtitleCodec? codec = factory.CreateSubtitleCodec(name);

        Assert.NotNull(codec);
        Assert.IsType<AssCodec>(codec);
    }

    [Fact]
    public void CodecFactory_CreateSubtitleCodec_Copy_ReturnsSubtitleCopyCodec()
    {
        CodecFactory factory = new();

        ISubtitleCodec? codec = factory.CreateSubtitleCodec("copy");

        Assert.NotNull(codec);
        Assert.IsType<SubtitleCopyCodec>(codec);
    }

    #endregion

    #region CodecFactory Preset Tests

    [Fact]
    public void CodecFactory_CreateVideoCodecWithPreset_UltraFast_SetsCorrectPreset()
    {
        CodecFactory factory = new();

        IVideoCodec codec = factory.CreateVideoCodecWithPreset("libx264", VideoPreset.UltraFast);

        Assert.Equal("ultrafast", codec.Preset);
        Assert.Equal(28, codec.Crf);
    }

    [Fact]
    public void CodecFactory_CreateVideoCodecWithPreset_Balanced_SetsCorrectPreset()
    {
        CodecFactory factory = new();

        IVideoCodec codec = factory.CreateVideoCodecWithPreset("libx264", VideoPreset.Balanced);

        Assert.Equal("medium", codec.Preset);
        Assert.Equal(22, codec.Crf);
    }

    [Fact]
    public void CodecFactory_CreateVideoCodecWithPreset_HighQuality_SetsCorrectPreset()
    {
        CodecFactory factory = new();

        IVideoCodec codec = factory.CreateVideoCodecWithPreset("libx264", VideoPreset.HighQuality);

        Assert.Contains(codec.Preset, new[] { "slower", "slow" });
        Assert.Equal(18, codec.Crf);
    }

    [Fact]
    public void CodecFactory_CreateVideoCodecWithPreset_UnknownCodec_ThrowsException()
    {
        CodecFactory factory = new();

        Assert.Throws<ArgumentException>(() =>
            factory.CreateVideoCodecWithPreset("unknown_codec", VideoPreset.Balanced));
    }

    #endregion

    #region ContainerFactory Tests

    [Theory]
    [InlineData("hls")]
    [InlineData("m3u8")]
    public void ContainerFactory_CreateContainer_Hls_ReturnsHlsContainer(string format)
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer(format);

        Assert.NotNull(container);
        Assert.IsType<HlsContainer>(container);
    }

    [Fact]
    public void ContainerFactory_CreateContainer_Mp4_ReturnsMp4Container()
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer("mp4");

        Assert.NotNull(container);
        Assert.IsType<Mp4Container>(container);
    }

    [Theory]
    [InlineData("fmp4")]
    [InlineData("fragmented-mp4")]
    public void ContainerFactory_CreateContainer_Fmp4_ReturnsFragmentedMp4Container(string format)
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer(format);

        Assert.NotNull(container);
        Assert.IsType<FragmentedMp4Container>(container);
    }

    [Theory]
    [InlineData("mkv")]
    [InlineData("matroska")]
    public void ContainerFactory_CreateContainer_Mkv_ReturnsMkvContainer(string format)
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer(format);

        Assert.NotNull(container);
        Assert.IsType<MkvContainer>(container);
    }

    [Fact]
    public void ContainerFactory_CreateContainer_WebM_ReturnsWebMContainer()
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer("webm");

        Assert.NotNull(container);
        Assert.IsType<WebMContainer>(container);
    }

    [Fact]
    public void ContainerFactory_CreateContainer_Unknown_ReturnsNull()
    {
        ContainerFactory factory = new();

        IContainer? container = factory.CreateContainer("unknown_format");

        Assert.Null(container);
    }

    [Fact]
    public void ContainerFactory_CreateHlsContainer_SetsSegmentDuration()
    {
        ContainerFactory factory = new();

        IHlsContainer container = factory.CreateHlsContainer(segmentDuration: 6);

        Assert.Equal(6, container.SegmentDuration);
    }

    [Fact]
    public void ContainerFactory_CreateHlsContainer_SetsPlaylistType()
    {
        ContainerFactory factory = new();

        IHlsContainer container = factory.CreateHlsContainer(playlistType: HlsPlaylistType.Event);

        Assert.Equal(HlsPlaylistType.Event, container.PlaylistType);
    }

    [Fact]
    public void ContainerFactory_CreateStreamableMp4_HasFastStart()
    {
        ContainerFactory factory = new();

        IContainer container = factory.CreateStreamableMp4();

        Assert.IsType<Mp4Container>(container);
        Assert.True(((Mp4Container)container).FastStart);
    }

    [Fact]
    public void ContainerFactory_CreateDashCompatibleMp4_SetsFragmentDuration()
    {
        ContainerFactory factory = new();

        IContainer container = factory.CreateDashCompatibleMp4(fragmentDurationMs: 5000);

        Assert.IsType<FragmentedMp4Container>(container);
        Assert.Equal(5000, ((FragmentedMp4Container)container).FragmentDuration);
    }

    [Fact]
    public void ContainerFactory_AvailableContainers_ContainsCommonFormats()
    {
        ContainerFactory factory = new();

        Assert.Contains("hls", factory.AvailableContainers);
        Assert.Contains("mp4", factory.AvailableContainers);
        Assert.Contains("mkv", factory.AvailableContainers);
        Assert.Contains("webm", factory.AvailableContainers);
    }

    #endregion
}
