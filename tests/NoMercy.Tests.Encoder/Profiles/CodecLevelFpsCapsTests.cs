using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// Codec-level max-luma-samples-per-second tables. These values come from the
/// codec specs (H.264 Table A-1, H.265 Table A-1, WebM VP9 §1) and must match
/// exactly — encoders reject streams that declare a level smaller than their
/// content needs, so a wrong table entry produces a confusing "encoder
/// refused the stream" error at runtime.
/// </summary>
public class CodecLevelFpsCapsTests
{
    // ── H.264 spec values ───────────────────────────────────────────────────

    [Theory]
    [InlineData("4.0", 62_914_560L)] // 1080p30 needs ~62M
    [InlineData("4.1", 62_914_560L)] // Same as 4.0
    [InlineData("4.2", 133_693_440L)] // 1080p60
    [InlineData("5.0", 150_994_944L)] // Up to ~4K30 in theory
    [InlineData("5.1", 251_658_240L)] // 4K30 typical declared
    [InlineData("5.2", 530_841_600L)] // 4K60
    [InlineData("6.0", 1_069_547_520L)] // 8K30
    [InlineData("6.2", 4_278_190_080L)] // 8K120
    public void H264_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(VideoCodecType.H264, level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── HEVC spec values ────────────────────────────────────────────────────

    [Theory]
    [InlineData("3.1", 33_177_600L)] // 720p30
    [InlineData("4.0", 66_846_720L)] // 1080p30 typical
    [InlineData("4.1", 133_693_440L)] // 1080p60
    [InlineData("5.0", 267_386_880L)] // 4K30
    [InlineData("5.1", 534_773_760L)] // 4K60
    [InlineData("5.2", 1_069_547_520L)] // 8K30
    public void Hevc_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(VideoCodecType.H265, level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── VP9 spec values ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("3.1", 36_864_000L)]
    [InlineData("4", 83_558_400L)]
    [InlineData("5", 311_951_360L)]
    [InlineData("5.1", 588_251_136L)]
    public void Vp9_Lookup_SpecValues(string level, long expected)
    {
        CodecLevelFpsCaps
            .Lookup(VideoCodecType.Vp9, level)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(c => c.MaxLumaSamplesPerSec == expected);
    }

    // ── Lookup edge cases ───────────────────────────────────────────────────

    [Fact]
    public void Lookup_UnknownLevel_ReturnsNull()
    {
        CodecLevelFpsCaps.Lookup(VideoCodecType.H264, "9.9").Should().BeNull();
    }

    [Fact]
    public void Lookup_UnsupportedCodec_ReturnsNull()
    {
        // AV1 / Copy / etc. have no entries in this table yet.
        CodecLevelFpsCaps.Lookup(VideoCodecType.Av1, "5.0").Should().BeNull();
        CodecLevelFpsCaps.Lookup(VideoCodecType.Copy, "5.0").Should().BeNull();
    }

    [Fact]
    public void Lookup_CaseInsensitive()
    {
        // Level strings can come from user input or DB rows — accept any case.
        CodecLevelFpsCaps.Lookup(VideoCodecType.H264, "4.0").Should().NotBeNull();
        CodecLevelFpsCaps
            .Lookup(VideoCodecType.H264, "4.0".ToUpperInvariant())
            .Should()
            .NotBeNull();
    }

    // ── FindNextFit ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VideoCodecType.H264, 62_914_560L, "4.0")] // Exact-match boundary
    [InlineData(VideoCodecType.H264, 70_000_000L, "4.2")] // Bumps to next level
    [InlineData(VideoCodecType.H264, 530_841_600L, "5.2")] // 4K60ish exact-match boundary
    [InlineData(VideoCodecType.H265, 100_000_000L, "4.1")] // HEVC 4.1 = 133M
    [InlineData(VideoCodecType.H265, 500_000_000L, "5.1")] // 4K60 HEVC
    public void FindNextFit_PicksFirstSufficientLevel(
        VideoCodecType codec,
        long required,
        string expected
    )
    {
        CodecLevelFpsCaps
            .FindNextFit(codec, required)
            .Should()
            .NotBeNull()
            .And.Match<CodecLevelFpsCaps.LevelCap>(c => c.Level == expected);
    }

    [Fact]
    public void FindNextFit_ExceedsHighestLevel_ReturnsNull()
    {
        // H.264 6.2 caps at 4.28B; ask for more.
        CodecLevelFpsCaps.FindNextFit(VideoCodecType.H264, 5_000_000_000L).Should().BeNull();
    }

    [Fact]
    public void FindNextFit_UnsupportedCodec_ReturnsNull()
    {
        CodecLevelFpsCaps.FindNextFit(VideoCodecType.Av1, 100_000_000L).Should().BeNull();
    }

    // ── Table integrity ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(VideoCodecType.H264)]
    [InlineData(VideoCodecType.H265)]
    [InlineData(VideoCodecType.Vp9)]
    public void Tables_AreMonotonicallyIncreasing(VideoCodecType codec)
    {
        // FindNextFit relies on tables being ordered low→high so the first
        // match is the smallest sufficient level. Any out-of-order entry
        // breaks that contract silently.
        IReadOnlyList<CodecLevelFpsCaps.LevelCap> table = codec switch
        {
            VideoCodecType.H264 => CodecLevelFpsCaps.H264,
            VideoCodecType.H265 => CodecLevelFpsCaps.Hevc,
            VideoCodecType.Vp9 => CodecLevelFpsCaps.Vp9,
            _ => [],
        };

        for (int i = 1; i < table.Count; i++)
        {
            // Allow equal (4.0 / 4.1 in H.264 share the same cap by design).
            table[i]
                .MaxLumaSamplesPerSec.Should()
                .BeGreaterThanOrEqualTo(
                    table[i - 1].MaxLumaSamplesPerSec,
                    $"{codec} table entry {table[i].Level} must be >= preceding "
                        + $"{table[i - 1].Level}"
                );
        }
    }
}
