using NoMercy.Encoder.Codecs;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// HLS CODECS strings for EXT-X-STREAM-INF / EXT-X-MEDIA tags follow strict
/// RFC 6381 / ISO 14496-15 / AV1 ISOBMFF binding formats. Wrong characters
/// or wrong levels make players reject the variant entirely — every value
/// here is a contract with player implementations.
/// </summary>
public class HlsCodecsStringBuilderTests
{
    // ── H.264 (avc1.PPCCLL) ────────────────────────────────────────────────

    [Theory]
    [InlineData("baseline", "3.0", "avc1.42401E")] // PP=0x42, CC=0x40 (constraint), LL=0x1E (30)
    [InlineData("main", "4.0", "avc1.4D0028")] // PP=0x4D, CC=0x00, LL=0x28 (40)
    [InlineData("high", "4.1", "avc1.640029")] // PP=0x64, CC=0x00, LL=0x29 (41)
    [InlineData("high10", "5.0", "avc1.6E0032")] // PP=0x6E (High 10), LL=0x32 (50)
    [InlineData("high422", "5.1", "avc1.7A0033")] // PP=0x7A (High 4:2:2), LL=0x33 (51)
    [InlineData("high444", "5.2", "avc1.F40034")] // PP=0xF4 (High 4:4:4), LL=0x34 (52)
    public void ForH264_KnownProfileLevels(string profile, string level, string expected)
    {
        HlsCodecsStringBuilder.ForH264(profile, level).Should().Be(expected);
    }

    [Fact]
    public void ForH264_NullProfile_DefaultsToHigh()
    {
        HlsCodecsStringBuilder.ForH264(null, "4.0").Should().Be("avc1.640028");
    }

    [Fact]
    public void ForH264_NullLevel_DefaultsTo4_0()
    {
        HlsCodecsStringBuilder.ForH264("high", null).Should().Be("avc1.640028");
    }

    [Theory]
    [InlineData("constrained baseline", "avc1.42401E")] // Marketing-name alias
    [InlineData("hi10p", "avc1.6E001E")] // x264-style alias (10-bit)
    public void ForH264_AcceptsCommonAliases(string profile, string expectedPrefix)
    {
        HlsCodecsStringBuilder.ForH264(profile, "3.0").Should().Be(expectedPrefix);
    }

    // ── HEVC (hvc1.P.CC.LLL.B0) ────────────────────────────────────────────

    [Theory]
    [InlineData("3.1", false, "hvc1.1.6.L93.B0")] // SDR Main
    [InlineData("4.0", true, "hvc1.2.4.L120.B0")] // HDR Main10
    [InlineData("5.1", false, "hvc1.1.6.L153.B0")] // SDR Main, 4K
    [InlineData("5.1", true, "hvc1.2.4.L153.B0")] // HDR Main10, 4K
    public void ForHevc_KnownLevels(string level, bool tenBit, string expected)
    {
        HlsCodecsStringBuilder.ForHevc("main", level, tenBit).Should().Be(expected);
    }

    [Fact]
    public void ForHevc_NullLevel_DefaultsByBitDepth()
    {
        HlsCodecsStringBuilder.ForHevc("main", null, tenBit: false).Should().Be("hvc1.1.6.L93.B0");
        HlsCodecsStringBuilder.ForHevc("main", null, tenBit: true).Should().Be("hvc1.2.4.L120.B0");
    }

    // ── AV1 (av01.0.LLT.DD) ────────────────────────────────────────────────

    [Theory]
    [InlineData("4.0", false, "av01.0.08M.08")] // 1080p AV1 8-bit
    [InlineData("5.0", false, "av01.0.12M.08")] // 4K AV1 8-bit
    [InlineData("5.3", true, "av01.0.15M.10")] // 4K AV1 10-bit
    [InlineData("3.0", false, "av01.0.04M.08")] // 720p AV1 8-bit
    public void ForAv1_KnownLevels(string level, bool tenBit, string expected)
    {
        HlsCodecsStringBuilder.ForAv1(level, tenBit).Should().Be(expected);
    }

    [Fact]
    public void ForAv1_NullLevel_DefaultsByBitDepth()
    {
        HlsCodecsStringBuilder.ForAv1(null, tenBit: false).Should().Be("av01.0.08M.08");
        HlsCodecsStringBuilder.ForAv1(null, tenBit: true).Should().Be("av01.0.15M.10");
    }

    // ── Audio codec strings ────────────────────────────────────────────────

    [Theory]
    [InlineData("aac", false, "mp4a.40.2")] // AAC-LC
    [InlineData("aac", true, "mp4a.40.5")] // HE-AAC
    [InlineData("libfdk_aac", false, "mp4a.40.2")]
    [InlineData("libfdk_aac", true, "mp4a.40.5")]
    [InlineData("ac3", false, "ac-3")]
    [InlineData("eac3", false, "ec-3")]
    [InlineData("libopus", false, "opus")]
    [InlineData("opus", false, "opus")]
    [InlineData("unknown_codec", false, "mp4a.40.2")] // Defaults to AAC-LC
    public void AudioCodecString_KnownEncoders(string encoder, bool heAac, string expected)
    {
        HlsCodecsStringBuilder.AudioCodecString(encoder, heAac).Should().Be(expected);
    }

    // ── VideoCodecString dispatch via classifier ────────────────────────────

    [Theory]
    [InlineData("libx264", "main", "4.0", false, "avc1.4D0028")]
    [InlineData("h264_nvenc", "high", "4.1", false, "avc1.640029")]
    [InlineData("libx265", "main", "3.1", false, "hvc1.1.6.L93.B0")]
    [InlineData("hevc_nvenc", "main10", "4.0", true, "hvc1.2.4.L120.B0")]
    [InlineData("libsvtav1", null, "5.0", false, "av01.0.12M.08")]
    [InlineData("av1_nvenc", null, "5.3", true, "av01.0.15M.10")]
    [InlineData("libvpx-vp9", null, null, false, "vp09.00.41.08")]
    [InlineData("vp9_qsv", null, null, true, "vp09.00.41.10")]
    public void VideoCodecString_ClassifiesEncoderCorrectly(
        string encoder,
        string? profile,
        string? level,
        bool tenBit,
        string expected
    )
    {
        HlsCodecsStringBuilder
            .VideoCodecString(encoder, profile, level, tenBit)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void VideoCodecString_UnknownEncoder_FallsBackToH264HighL4_0()
    {
        HlsCodecsStringBuilder
            .VideoCodecString("totally_unknown_encoder", null, null, false)
            .Should()
            .Be("avc1.640028");
    }
}
