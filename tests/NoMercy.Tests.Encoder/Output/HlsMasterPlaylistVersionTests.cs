using NoMercy.Encoder.Output;

namespace NoMercy.Tests.Encoder.Output;

public class HlsMasterPlaylistVersionTests
{
    [Fact]
    public void ComputeMasterVersion_returns_3_for_basic_mpegts()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: false,
            hasChapterDateRanges: false
        );

        Assert.Equal(3, version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_6_when_subs_group_present()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: false,
            hasChapterDateRanges: false
        );

        Assert.Equal(6, version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_7_when_fmp4()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: true,
            hasChapterDateRanges: false
        );

        Assert.Equal(7, version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_8_when_chapter_dateranges()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: false,
            hasChapterDateRanges: true
        );

        Assert.Equal(8, version);
    }

    [Fact]
    public void ComputeMasterVersion_picks_highest_when_multiple_features()
    {
        // subs + fmp4 → 7
        int versionSubsFmp4 = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: true,
            hasChapterDateRanges: false
        );

        Assert.Equal(7, versionSubsFmp4);

        // subs + fmp4 + chapters → 8
        int versionAll = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: true,
            hasChapterDateRanges: true
        );

        Assert.Equal(8, versionAll);
    }
}
