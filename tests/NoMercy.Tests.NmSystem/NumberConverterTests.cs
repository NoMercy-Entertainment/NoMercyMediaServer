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
[Trait(name: "Category", value: "Unit")]
public class NumberConverterTests
{
    [Theory]
    [InlineData(data: [1920, 1080, "16:9"])]
    [InlineData(data: [1280, 720, "16:9"])]
    [InlineData(data: [3840, 2160, "16:9"])]
    [InlineData(data: [640, 480, "4:3"])]
    [InlineData(data: [500, 500, "1:1"])]
    public void NormalizeAspectRatio_SnapsCommonRatios(int width, int height, string expected)
    {
        NumberConverter.NormalizeAspectRatio(width: width, height: height).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [0, 1080])]
    [InlineData(data: [1920, 0])]
    [InlineData(data: [-1920, 1080])]
    public void NormalizeAspectRatio_GuardsNonPositiveDimensions(int width, int height)
    {
        NumberConverter.NormalizeAspectRatio(width: width, height: height).Should().Be(expected: "1:1");
    }

    [Fact]
    public void NormalizeAspectRatio_FallsBackToReducedFraction()
    {
        // 5:3 (1.667) is not within tolerance of any snapped ratio, so it stays
        // as the reduced fraction.
        NumberConverter.NormalizeAspectRatio(width: 1000, height: 600).Should().Be(expected: "5:3");
    }

    [Fact]
    public void NormalizeAspectRatio_RoundsDoubleOverload()
    {
        NumberConverter.NormalizeAspectRatio(width: 1919.6, height: 1080.4).Should().Be(expected: "16:9");
    }

    [Theory]
    [InlineData(data: ["0", "zero"])]
    [InlineData(data: ["7", "seven"])]
    [InlineData(data: ["13", "thirteen"])]
    [InlineData(data: ["21", "twenty one"])]
    [InlineData(data: ["100", "one hundred"])]
    [InlineData(data: ["1234", "one thousand two hundred thirty four"])]
    [InlineData(data: ["1000000", "one million"])]
    public void ToName_ConvertsNumbersToWords(string input, string expected)
    {
        input.ToName().Should().Be(expected: expected);
    }

    [Fact]
    public void ToName_ConvertsNumbersEmbeddedInText()
    {
        "I have 21 cats".ToName().Should().Be(expected: "I have twenty one cats");
    }

    [Fact]
    public void ToName_LeavesNonNumericTextUntouched()
    {
        "no numbers here".ToName().Should().Be(expected: "no numbers here");
    }

    [Theory]
    [InlineData(data: ["999", "nine hundred ninety nine"])]
    [InlineData(data: ["1000", "one thousand"])]
    [InlineData(data: ["10000", "ten thousand"])]
    [InlineData(data: ["999999", "nine hundred ninety nine thousand nine hundred ninety nine"])]
    [InlineData(data: ["1000000000", "one billion"])]
    [InlineData(data: ["999999999", "nine hundred ninety nine million nine hundred ninety nine thousand nine hundred ninety nine"])]
    [InlineData(data: ["1000000001", "one billion one"])]
    public void ToName_ConvertesNumbersToWordsForLargeNumbers(string input, string expected)
    {
        input.ToName().Should().Be(expected: expected);
    }

    [Fact]
    public void NormalizeAspectRatio_WithNegativeHeight_ReturnsOne1()
    {
        NumberConverter.NormalizeAspectRatio(width: 1920, height: -1080).Should().Be(expected: "1:1");
    }

    [Fact]
    public void NormalizeAspectRatio_5To4Ratio()
    {
        NumberConverter.NormalizeAspectRatio(width: 500, height: 400).Should().Be(expected: "5:4");
    }

    [Fact]
    public void NormalizeAspectRatio_21To9Ratio()
    {
        NumberConverter.NormalizeAspectRatio(width: 2520, height: 1080).Should().Be(expected: "21:9");
    }

    [Fact]
    public void NormalizeAspectRatio_32To9Ratio()
    {
        NumberConverter.NormalizeAspectRatio(width: 1920, height: 540).Should().Be(expected: "32:9");
    }

    [Fact]
    public void NormalizeAspectRatio_3To2Ratio()
    {
        NumberConverter.NormalizeAspectRatio(width: 900, height: 600).Should().Be(expected: "3:2");
    }

    [Fact]
    public void NormalizeAspectRatio_ArbitraryRatio()
    {
        NumberConverter.NormalizeAspectRatio(width: 1024, height: 768).Should().Be(expected: "4:3");
    }
}
