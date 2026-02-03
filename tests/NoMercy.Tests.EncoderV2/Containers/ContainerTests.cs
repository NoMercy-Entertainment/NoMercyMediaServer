using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Codecs.Audio;
using NoMercy.EncoderV2.Codecs.Video;
using NoMercy.EncoderV2.Containers;

namespace NoMercy.Tests.EncoderV2.Containers;

public class ContainerTests
{
    #region HlsContainer Tests

    [Fact]
    public void HlsContainer_DefaultSettings_BuildsCorrectArguments()
    {
        HlsContainer container = new();

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-f", args);
        Assert.Contains("hls", args);
    }

    [Fact]
    public void HlsContainer_WithSegmentDuration_IncludesHlsTime()
    {
        HlsContainer container = new() { SegmentDuration = 6 };

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-hls_time", args);
        Assert.Contains("6", args);
    }

    [Fact]
    public void HlsContainer_VodPlaylistType_IncludesPlaylistType()
    {
        HlsContainer container = new() { PlaylistType = HlsPlaylistType.Vod };

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-hls_playlist_type", args);
        Assert.Contains("vod", args);
    }

    [Fact]
    public void HlsContainer_EventPlaylistType_IncludesPlaylistType()
    {
        HlsContainer container = new() { PlaylistType = HlsPlaylistType.Event };

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-hls_playlist_type", args);
        Assert.Contains("event", args);
    }

    [Fact]
    public void HlsContainer_SupportsStreaming()
    {
        HlsContainer container = new();

        Assert.True(container.SupportsStreaming);
    }

    [Fact]
    public void HlsContainer_CorrectExtension()
    {
        HlsContainer container = new();

        Assert.Equal(".m3u8", container.Extension);
    }

    [Fact]
    public void HlsContainer_CorrectMimeType()
    {
        HlsContainer container = new();

        Assert.Equal("application/vnd.apple.mpegurl", container.MimeType);
    }

    [Fact]
    public void HlsContainer_CompatibleWithH264()
    {
        HlsContainer container = new();

        Assert.Contains("libx264", container.CompatibleVideoCodecs);
        Assert.Contains("h264_nvenc", container.CompatibleVideoCodecs);
    }

    [Fact]
    public void HlsContainer_CompatibleWithH265()
    {
        HlsContainer container = new();

        Assert.Contains("libx265", container.CompatibleVideoCodecs);
        Assert.Contains("hevc_nvenc", container.CompatibleVideoCodecs);
    }

    [Fact]
    public void HlsContainer_CompatibleWithAac()
    {
        HlsContainer container = new();

        Assert.Contains("aac", container.CompatibleAudioCodecs);
    }

    [Fact]
    public void HlsContainer_ValidateCodecs_ValidCombination_ReturnsSuccess()
    {
        HlsContainer container = new();
        H264Codec video = new();
        AacCodec audio = new();

        ValidationResult result = container.ValidateCodecs(video, audio, null);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void HlsContainer_GetBitstreamFilter_H264_ReturnsCorrectFilter()
    {
        string filter = HlsContainer.GetBitstreamFilter("libx264");

        Assert.Equal("h264_mp4toannexb", filter);
    }

    [Fact]
    public void HlsContainer_GetBitstreamFilter_H265_ReturnsCorrectFilter()
    {
        string filter = HlsContainer.GetBitstreamFilter("libx265");

        Assert.Equal("hevc_mp4toannexb", filter);
    }

    #endregion

    #region Mp4Container Tests

    [Fact]
    public void Mp4Container_DefaultSettings_BuildsCorrectArguments()
    {
        Mp4Container container = new();

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-f", args);
        Assert.Contains("mp4", args);
    }

    [Fact]
    public void Mp4Container_WithFastStart_IncludesMovFlags()
    {
        Mp4Container container = new() { FastStart = true };

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-movflags", args);
        Assert.Contains("+faststart", args);
    }

    [Fact]
    public void Mp4Container_SupportsStreaming()
    {
        Mp4Container container = new();

        Assert.True(container.SupportsStreaming);
    }

    [Fact]
    public void Mp4Container_CorrectExtension()
    {
        Mp4Container container = new();

        Assert.Equal(".mp4", container.Extension);
    }

    [Fact]
    public void Mp4Container_CorrectMimeType()
    {
        Mp4Container container = new();

        Assert.Equal("video/mp4", container.MimeType);
    }

    [Fact]
    public void Mp4Container_CompatibleWithAv1()
    {
        Mp4Container container = new();

        Assert.Contains("libaom-av1", container.CompatibleVideoCodecs);
        Assert.Contains("libsvtav1", container.CompatibleVideoCodecs);
    }

    [Fact]
    public void FragmentedMp4Container_BuildsCorrectArguments()
    {
        FragmentedMp4Container container = new();

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-movflags", args);
    }

    #endregion

    #region MkvContainer Tests

    [Fact]
    public void MkvContainer_DefaultSettings_BuildsCorrectArguments()
    {
        MkvContainer container = new();

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-f", args);
        Assert.Contains("matroska", args);
    }

    [Fact]
    public void MkvContainer_CorrectExtension()
    {
        MkvContainer container = new();

        Assert.Equal(".mkv", container.Extension);
    }

    [Fact]
    public void MkvContainer_SupportsAllCodecs()
    {
        MkvContainer container = new();

        // MKV supports virtually everything
        Assert.Contains("libx264", container.CompatibleVideoCodecs);
        Assert.Contains("libx265", container.CompatibleVideoCodecs);
        Assert.Contains("libaom-av1", container.CompatibleVideoCodecs);
        Assert.Contains("libvpx-vp9", container.CompatibleVideoCodecs);
        Assert.Contains("aac", container.CompatibleAudioCodecs);
        Assert.Contains("flac", container.CompatibleAudioCodecs);
        Assert.Contains("libopus", container.CompatibleAudioCodecs);
    }

    #endregion

    #region WebMContainer Tests

    [Fact]
    public void WebMContainer_DefaultSettings_BuildsCorrectArguments()
    {
        WebMContainer container = new();

        IReadOnlyList<string> args = container.BuildArguments();

        Assert.Contains("-f", args);
        Assert.Contains("webm", args);
    }

    [Fact]
    public void WebMContainer_CorrectExtension()
    {
        WebMContainer container = new();

        Assert.Equal(".webm", container.Extension);
    }

    [Fact]
    public void WebMContainer_CorrectMimeType()
    {
        WebMContainer container = new();

        Assert.Equal("video/webm", container.MimeType);
    }

    [Fact]
    public void WebMContainer_OnlySupportsVp9AndAv1()
    {
        WebMContainer container = new();

        Assert.Contains("libvpx-vp9", container.CompatibleVideoCodecs);
        Assert.Contains("libaom-av1", container.CompatibleVideoCodecs);
        Assert.DoesNotContain("libx264", container.CompatibleVideoCodecs);
        Assert.DoesNotContain("libx265", container.CompatibleVideoCodecs);
    }

    [Fact]
    public void WebMContainer_OnlySupportsOpusAndVorbis()
    {
        WebMContainer container = new();

        Assert.Contains("libopus", container.CompatibleAudioCodecs);
        Assert.Contains("libvorbis", container.CompatibleAudioCodecs);
        Assert.DoesNotContain("aac", container.CompatibleAudioCodecs);
    }

    [Fact]
    public void WebMContainer_ValidateCodecs_IncompatibleVideo_ReturnsError()
    {
        WebMContainer container = new();
        H264Codec video = new();

        ValidationResult result = container.ValidateCodecs(video, null, null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("VP9") || e.Contains("AV1"));
    }

    [Fact]
    public void WebMContainer_ValidateCodecs_IncompatibleAudio_ReturnsError()
    {
        WebMContainer container = new();
        AacCodec audio = new();

        ValidationResult result = container.ValidateCodecs(null, audio, null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void WebMContainer_ValidateCodecs_ValidCombination_ReturnsSuccess()
    {
        WebMContainer container = new();
        Vp9Codec video = new();
        OpusCodec audio = new();

        ValidationResult result = container.ValidateCodecs(video, audio, null);

        Assert.True(result.IsValid);
    }

    #endregion
}
