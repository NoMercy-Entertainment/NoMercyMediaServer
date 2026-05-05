using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class TmdbDiscMatcherTests
{
    [Theory]
    [InlineData("Avatar_Book_1_Disc_1", "Avatar Book 1")]
    [InlineData("Avatar_Book_1_Disc_2", "Avatar Book 1")]
    [InlineData("Avatar.Book.1.Disc.1", "Avatar Book 1")]
    [InlineData("Avatar-Book-1-Disc-1", "Avatar Book 1")]
    [InlineData("LORD_OF_THE_RINGS", "LORD OF THE RINGS")]
    [InlineData("Wall-E", "Wall E")]
    [InlineData("THE_MATRIX_DISC_1", "THE MATRIX")]
    [InlineData("Frozen", "Frozen")]
    public void NormalizeLabel_StripsSeparatorsAndDiscSuffix(string input, string expected)
    {
        TmdbDiscMatcher.NormalizeLabel(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeLabel_EmptyInput_ReturnsEmpty()
    {
        TmdbDiscMatcher.NormalizeLabel("").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_OnlyDiscSuffix_ReturnsEmpty()
    {
        // " disc 1" alone → suffix stripped, just whitespace, trimmed to ""
        TmdbDiscMatcher.NormalizeLabel(" disc 1").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_DoesNotStripDiscMidWord()
    {
        // "Discovery Channel Disc 1" — only the trailing "Disc 1" should go
        TmdbDiscMatcher.NormalizeLabel("Discovery Channel Disc 1").Should().Be("Discovery Channel");
    }

    [Fact]
    public void NormalizeLabel_PreservesNumbersInTitle()
    {
        // "Star Trek 2 Disc 1" → "Star Trek 2" (the 2 is part of the title,
        // only the trailing disc-N gets stripped)
        TmdbDiscMatcher.NormalizeLabel("Star_Trek_2_Disc_1").Should().Be("Star Trek 2");
    }
}
