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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins <see cref="NumberConverter"/>: aspect-ratio normalization (GCD + common
/// ratio snapping) used for video metadata, and the integer-to-words conversion
/// reached through <see cref="StringExtensions.ToName"/>.
/// </summary>
[Trait("Category", "Unit")]
public class NumberConverterTests
{
    [Theory]
    [InlineData([1920, 1080, "16:9"])]
    [InlineData([1280, 720, "16:9"])]
    [InlineData([3840, 2160, "16:9"])]
    [InlineData([640, 480, "4:3"])]
    [InlineData([500, 500, "1:1"])]
    public void NormalizeAspectRatio_SnapsCommonRatios(int width, int height, string expected)
    {
        NumberConverter.NormalizeAspectRatio(width, height).Should().Be(expected);
    }

    [Theory]
    [InlineData([0, 1080])]
    [InlineData([1920, 0])]
    [InlineData([-1920, 1080])]
    public void NormalizeAspectRatio_GuardsNonPositiveDimensions(int width, int height)
    {
        NumberConverter.NormalizeAspectRatio(width, height).Should().Be("1:1");
    }

    [Fact]
    public void NormalizeAspectRatio_FallsBackToReducedFraction()
    {
        // 5:3 (1.667) is not within tolerance of any snapped ratio, so it stays
        // as the reduced fraction.
        NumberConverter.NormalizeAspectRatio(1000, 600).Should().Be("5:3");
    }

    [Fact]
    public void NormalizeAspectRatio_RoundsDoubleOverload()
    {
        NumberConverter.NormalizeAspectRatio(1919.6, 1080.4).Should().Be("16:9");
    }

    [Theory]
    [InlineData(["0", "zero"])]
    [InlineData(["7", "seven"])]
    [InlineData(["13", "thirteen"])]
    [InlineData(["21", "twenty one"])]
    [InlineData(["100", "one hundred"])]
    [InlineData(["1234", "one thousand two hundred thirty four"])]
    [InlineData(["1000000", "one million"])]
    public void ToName_ConvertsNumbersToWords(string input, string expected)
    {
        input.ToName().Should().Be(expected);
    }

    [Fact]
    public void ToName_ConvertsNumbersEmbeddedInText()
    {
        "I have 21 cats".ToName().Should().Be("I have twenty one cats");
    }

    [Fact]
    public void ToName_LeavesNonNumericTextUntouched()
    {
        "no numbers here".ToName().Should().Be("no numbers here");
    }

    [Theory]
    [InlineData(["999", "nine hundred ninety nine"])]
    [InlineData(["1000", "one thousand"])]
    [InlineData(["10000", "ten thousand"])]
    [InlineData(["999999", "nine hundred ninety nine thousand nine hundred ninety nine"])]
    [InlineData(["1000000000", "one billion"])]
    [InlineData(["999999999", "nine hundred ninety nine million nine hundred ninety nine thousand nine hundred ninety nine"])]
    [InlineData(["1000000001", "one billion one"])]
    public void ToName_ConvertesNumbersToWordsForLargeNumbers(string input, string expected)
    {
        input.ToName().Should().Be(expected);
    }

    [Fact]
    public void NormalizeAspectRatio_WithNegativeHeight_ReturnsOne1()
    {
        NumberConverter.NormalizeAspectRatio(1920, -1080).Should().Be("1:1");
    }

    [Fact]
    public void NormalizeAspectRatio_5To4Ratio()
    {
        NumberConverter.NormalizeAspectRatio(500, 400).Should().Be("5:4");
    }

    [Fact]
    public void NormalizeAspectRatio_21To9Ratio()
    {
        NumberConverter.NormalizeAspectRatio(2520, 1080).Should().Be("21:9");
    }

    [Fact]
    public void NormalizeAspectRatio_32To9Ratio()
    {
        NumberConverter.NormalizeAspectRatio(1920, 540).Should().Be("32:9");
    }

    [Fact]
    public void NormalizeAspectRatio_3To2Ratio()
    {
        NumberConverter.NormalizeAspectRatio(900, 600).Should().Be("3:2");
    }

    [Fact]
    public void NormalizeAspectRatio_ArbitraryRatio()
    {
        NumberConverter.NormalizeAspectRatio(1024, 768).Should().Be("4:3");
    }
}
