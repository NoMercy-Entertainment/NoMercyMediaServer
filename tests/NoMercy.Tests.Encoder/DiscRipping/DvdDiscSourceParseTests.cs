using FluentAssertions;
using NoMercy.OpticalMedia.Sources.Dvd;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Synthetic stderr fixtures matching the formats libdvdread emits when
/// listing titles. Real DVD output will refine these — for now these lock
/// in the regex tolerance for the variations seen in the wild.
/// </summary>
public class DvdDiscSourceParseTests
{
    [Fact]
    public void ParseTitles_StandardFormat_ReturnsAll()
    {
        const string stderr =
            @"[dvdvideo] title 1, runtime 1:42:31, chapters 28
[dvdvideo] title 2, runtime 0:08:15, chapters 4
[dvdvideo] title 3, runtime 0:03:22, chapters 1";

        var titles = DvdDiscSource.ParseTitles(stderr);
        titles.Should().HaveCount(3);
        titles[0].Index.Should().Be(1);
        titles[0].Duration.Should().Be(new TimeSpan(1, 42, 31));
        titles[0].Chapters.Should().Be(28);
    }

    [Fact]
    public void ParseTitles_DurationVariant_StillMatches()
    {
        const string stderr = "[dvdvideo] title 5: duration 2:15:00, chapters 32";

        var titles = DvdDiscSource.ParseTitles(stderr);
        titles.Should().HaveCount(1);
        titles[0].Index.Should().Be(5);
        titles[0].Duration.Should().Be(new TimeSpan(2, 15, 0));
    }

    [Fact]
    public void ParseTitles_NoChaptersField_ReturnsZeroChapters()
    {
        const string stderr = "[dvdvideo] title 1, runtime 1:30:00";

        var titles = DvdDiscSource.ParseTitles(stderr);
        titles.Should().HaveCount(1);
        titles[0].Chapters.Should().Be(0);
    }

    [Fact]
    public void ParseTitles_DuplicateLines_Dedupes()
    {
        const string stderr =
            @"[dvdvideo] title 1, runtime 1:30:00, chapters 12
[dvdvideo] title 1, runtime 1:30:00, chapters 12";

        DvdDiscSource.ParseTitles(stderr).Should().HaveCount(1);
    }

    [Fact]
    public void ParseTitles_Empty_ReturnsEmpty()
    {
        DvdDiscSource.ParseTitles("").Should().BeEmpty();
        DvdDiscSource.ParseTitles(null!).Should().BeEmpty();
    }

    [Fact]
    public void ClassifyProtection_CssFail_ReturnsCss()
    {
        const string stderr = "[dvdread] css authentication failed";

        var p = DvdDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("CSS");
    }

    [Fact]
    public void ClassifyProtection_RegionLock_ReturnsRegionLock()
    {
        const string stderr = "[dvdread] region code mismatch";

        var p = DvdDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("RegionLock");
    }

    [Fact]
    public void ClassifyProtection_CleanOutput_ReturnsNull()
    {
        const string stderr = "[dvdvideo] title 1, runtime 1:30:00, chapters 12";

        DvdDiscSource.ClassifyProtection(stderr).Should().BeNull();
    }
}
