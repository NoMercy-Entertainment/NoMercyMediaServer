using NoMercy.NmSystem.Dto;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="AnimeParser.ParseAnimeFilename"/>: the fansub-style filename
/// parser that pulls group, name, season, episode, checksum and extension out of
/// bracketed anime release names, and falls back cleanly when nothing matches.
/// </summary>
[Trait("Category", "Unit")]
public class AnimeParserTests
{
    [Fact]
    public void ParseAnimeFilename_ExtractsEpisodeRelease()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename(
            "[HorribleSubs] Some Show - 01 [1080p][ABCD1234].mkv"
        );

        info.Group.Should().Be("HorribleSubs");
        info.Name.Trim().Should().Be("Some Show");
        info.Season.Should().BeNull();
        info.Episode.Should().Be(1);
        info.Checksum.Should().Be("ABCD1234");
        info.Extension.Should().Be("mkv");
    }

    [Fact]
    public void ParseAnimeFilename_ExtractsSeasonThenEpisode()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename("[Grp] Show S2 - 05 [DEADBEEF].mkv");

        info.Name.Trim().Should().Be("Show");
        info.Season.Should().Be(2);
        info.Episode.Should().Be(5);
        info.Checksum.Should().Be("DEADBEEF");
    }

    [Fact]
    public void ParseAnimeFilename_StripsVersionSuffix()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename("[Grp] Show - 05v2 [DEADBEEF].mkv");

        info.Episode.Should().Be(5);
        info.Season.Should().BeNull();
    }

    [Fact]
    public void ParseAnimeFilename_UsesUnderscoresAsSeparators()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename("[Grp]_Show_-_07_[ABCDEF12].mkv");

        info.Name.Trim().Should().Be("Show");
        info.Episode.Should().Be(7);
    }

    [Fact]
    public void ParseAnimeFilename_NoMatchReturnsFileNameOnly()
    {
        AnimeInfo info = AnimeParser.ParseAnimeFilename("random video.mp4");

        info.FileName.Should().Be("random video.mp4");
        info.Group.Should().BeEmpty();
        info.Season.Should().BeNull();
        info.Episode.Should().BeNull();
        info.Checksum.Should().BeNull();
    }
}
