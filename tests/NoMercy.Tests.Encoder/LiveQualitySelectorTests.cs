using NoMercy.Encoder.Live;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class LiveQualitySelectorTests
{
    [Fact]
    public void Select_JournalExample_4kHevcSourceForPhoneClient()
    {
        // Journal scenario: 4K HEVC source, phone client supports h264 + vp9 at 1080p, no HDR.
        SourceMetadata source = new()
        {
            VideoCodec = "hevc",
            AudioCodec = "ac3",
            Width = 3840,
            Height = 2160,
            IsHdr = true,
        };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["h264", "vp9"],
            SupportedAudioCodecs = ["aac", "opus"],
            MaxWidth = 1920,
            MaxHeight = 1080,
            HdrSupport = "none",
            SupportsHlsAes128 = true,
        };

        SelectedVariant variant = LiveQualitySelector.Select(source, client);

        Assert.Equal("vp9", variant.Codec); // VP9 preferred over H264 when both supported
        Assert.Equal("opus", variant.AudioCodec); // Opus preferred over AAC
        Assert.Equal(1920, variant.Width);
        Assert.Equal(1080, variant.Height);
        Assert.True(variant.ConvertHdrToSdr); // client doesn't support HDR
        Assert.True(variant.BitrateKbps > 0);
    }

    [Fact]
    public void Select_ClientWithAv1Support_PrefersAv1()
    {
        SourceMetadata source = new()
        {
            VideoCodec = "h264",
            Width = 1920,
            Height = 1080,
        };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["av1", "hevc", "h264"],
            MaxWidth = 1920,
            MaxHeight = 1080,
        };

        SelectedVariant variant = LiveQualitySelector.Select(source, client);
        Assert.Equal("av1", variant.Codec);
    }

    [Fact]
    public void Select_ClientOnlyH264_FallsBackToH264()
    {
        SourceMetadata source = new() { Width = 1280, Height = 720 };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["h264"],
            MaxWidth = 1920,
            MaxHeight = 1080,
        };

        Assert.Equal("h264", LiveQualitySelector.Select(source, client).Codec);
    }

    [Fact]
    public void Select_ClientWithNoSupportedCodecs_FallsBackToH264()
    {
        SourceMetadata source = new() { Width = 1280, Height = 720 };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["realmedia"], // unknown
            MaxWidth = 1920,
            MaxHeight = 1080,
        };

        Assert.Equal("h264", LiveQualitySelector.Select(source, client).Codec);
    }

    [Fact]
    public void Select_HdrClientWithHdrSource_KeepsHdr()
    {
        SourceMetadata source = new()
        {
            Width = 3840,
            Height = 2160,
            IsHdr = true,
        };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["hevc"],
            MaxWidth = 3840,
            MaxHeight = 2160,
            HdrSupport = "hdr10",
        };

        SelectedVariant variant = LiveQualitySelector.Select(source, client);
        Assert.False(variant.ConvertHdrToSdr);
    }

    [Fact]
    public void Select_SdrSource_NeverConverts()
    {
        SourceMetadata source = new()
        {
            Width = 1920,
            Height = 1080,
            IsHdr = false,
        };
        ClientCapabilities client = new()
        {
            SupportedVideoCodecs = ["h264"],
            MaxWidth = 1920,
            MaxHeight = 1080,
            HdrSupport = "none",
        };

        Assert.False(LiveQualitySelector.Select(source, client).ConvertHdrToSdr);
    }

    [Theory]
    [InlineData("hdr10", true)]
    [InlineData("HDR10", true)]
    [InlineData("hlg", true)]
    [InlineData("dovi", true)]
    [InlineData("dolby_vision", true)]
    [InlineData("pq", true)]
    [InlineData("none", false)]
    [InlineData("sdr", false)]
    [InlineData("", false)]
    [InlineData("unknown", false)]
    public void ClientSupportsHdr_RecognisesKnownLabels(string label, bool expected)
    {
        Assert.Equal(expected, LiveQualitySelector.ClientSupportsHdr(label));
    }

    [Theory]
    [InlineData(3840, 2160, 1920, 1080, 1920, 1080)] // 4K → 1080p
    [InlineData(1920, 1080, 1920, 1080, 1920, 1080)] // identity
    [InlineData(1920, 1080, 1280, 720, 1280, 720)] // 1080p → 720p
    [InlineData(1280, 720, 1920, 1080, 1280, 720)] // smaller than cap → unchanged
    [InlineData(3840, 1600, 1920, 1080, 1920, 800)] // ultrawide preserves aspect
    [InlineData(1080, 1920, 1080, 1920, 1080, 1920)] // portrait
    public void ClampDimensions_PreservesAspectRatio(
        int srcW,
        int srcH,
        int maxW,
        int maxH,
        int expectedW,
        int expectedH
    )
    {
        (int width, int height) = LiveQualitySelector.ClampDimensions(srcW, srcH, maxW, maxH);
        Assert.Equal(expectedW, width);
        Assert.Equal(expectedH, height);
    }

    [Fact]
    public void ClampDimensions_NoCap_ReturnsSource()
    {
        (int width, int height) = LiveQualitySelector.ClampDimensions(3840, 2160, 0, 0);
        Assert.Equal(3840, width);
        Assert.Equal(2160, height);
    }

    [Fact]
    public void ClampDimensions_ZeroSource_ReturnsZero()
    {
        Assert.Equal((0, 0), LiveQualitySelector.ClampDimensions(0, 0, 1920, 1080));
    }

    [Fact]
    public void ClampDimensions_AlwaysEven()
    {
        // 1921×1081 should clamp to even dimensions because chroma subsampling needs them.
        (int width, int height) = LiveQualitySelector.ClampDimensions(1921, 1081, 1921, 1081);
        Assert.Equal(0, width & 1);
        Assert.Equal(0, height & 1);
    }

    [Theory]
    [InlineData("h264", 3840, 2160, 12000)]
    [InlineData("hevc", 3840, 2160, 7500)] // efficient codec halves bitrate
    [InlineData("av1", 3840, 2160, 7500)]
    [InlineData("h264", 1920, 1080, 4500)]
    [InlineData("hevc", 1920, 1080, 3000)]
    [InlineData("h264", 1280, 720, 2500)]
    [InlineData("h264", 854, 480, 1100)]
    [InlineData("h264", 640, 360, 600)]
    public void EstimateBitrateKbps_TiersMatchAutoLadder(
        string codec,
        int width,
        int height,
        int expected
    )
    {
        Assert.Equal(expected, LiveQualitySelector.EstimateBitrateKbps(codec, width, height));
    }

    [Fact]
    public void EstimateBitrateKbps_ZeroDimensions_ReturnsFallback()
    {
        Assert.Equal(1000, LiveQualitySelector.EstimateBitrateKbps("h264", 0, 0));
    }
}
