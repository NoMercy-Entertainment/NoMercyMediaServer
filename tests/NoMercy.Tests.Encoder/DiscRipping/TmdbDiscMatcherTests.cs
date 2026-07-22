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
    [InlineData(data: ["Avatar_Book_1_Disc_1", "Avatar Book 1"])]
    [InlineData(data: ["Avatar_Book_1_Disc_2", "Avatar Book 1"])]
    [InlineData(data: ["Avatar.Book.1.Disc.1", "Avatar Book 1"])]
    [InlineData(data: ["Avatar-Book-1-Disc-1", "Avatar Book 1"])]
    [InlineData(data: ["LORD_OF_THE_RINGS", "LORD OF THE RINGS"])]
    [InlineData(data: ["Wall-E", "Wall E"])]
    [InlineData(data: ["THE_MATRIX_DISC_1", "THE MATRIX"])]
    [InlineData(data: ["Frozen", "Frozen"])]
    public void NormalizeLabel_StripsSeparatorsAndDiscSuffix(string input, string expected)
    {
        VideoDiscIdentifier.NormalizeLabel(label: input).Should().Be(expected: expected);
    }

    [Fact]
    public void NormalizeLabel_EmptyInput_ReturnsEmpty()
    {
        VideoDiscIdentifier.NormalizeLabel(label: "").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_OnlyDiscSuffix_ReturnsEmpty()
    {
        VideoDiscIdentifier.NormalizeLabel(label: " disc 1").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLabel_DoesNotStripDiscMidWord()
    {
        VideoDiscIdentifier
            .NormalizeLabel(label: "Discovery Channel Disc 1")
            .Should()
            .Be(expected: "Discovery Channel");
    }

    [Fact]
    public void NormalizeLabel_PreservesNumbersInTitle()
    {
        VideoDiscIdentifier.NormalizeLabel(label: "Star_Trek_2_Disc_1").Should().Be(expected: "Star Trek 2");
    }
}
