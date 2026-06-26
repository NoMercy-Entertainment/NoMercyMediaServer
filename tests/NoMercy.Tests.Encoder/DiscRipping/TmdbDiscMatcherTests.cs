// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
        VideoDiscIdentifier.NormalizeLabel(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeLabel_EmptyInput_ReturnsEmpty()
    {
        VideoDiscIdentifier.NormalizeLabel("").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_OnlyDiscSuffix_ReturnsEmpty()
    {
        VideoDiscIdentifier.NormalizeLabel(" disc 1").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_DoesNotStripDiscMidWord()
    {
        VideoDiscIdentifier
            .NormalizeLabel("Discovery Channel Disc 1")
            .Should()
            .Be("Discovery Channel");
    }

    [Fact]
    public void NormalizeLabel_PreservesNumbersInTitle()
    {
        VideoDiscIdentifier.NormalizeLabel("Star_Trek_2_Disc_1").Should().Be("Star Trek 2");
    }
}
