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

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using NoMercy.Launcher.ViewModels;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// REQUIREMENT: the log viewer colors/weights every row purely off the
/// server-supplied <c>level</c> (and, separately, its own <c>color</c> hex)
/// string — a case mismatch or an unrecognized level must fall back to the
/// neutral gray rather than throw or render invisible (white-on-white) text.
/// </summary>
public sealed class LevelClassConverterTests
{
    private static IBrush Convert(FuncValueConverter<string, IBrush> converter, string? level) =>
        (IBrush)converter.Convert(value: level, targetType: typeof(IBrush), parameter: null, culture: CultureInfo.InvariantCulture)!;

    [Theory]
    [InlineData(data: ["fatal", "#DC2626"])]
    [InlineData(data: ["FATAL", "#DC2626"])]
    [InlineData(data: ["error", "#EF4444"])]
    [InlineData(data: ["warning", "#EAB308"])]
    [InlineData(data: ["debug", "#6B7280"])]
    [InlineData(data: ["verbose", "#4B5563"])]
    public void LevelColorConverter_KnownLevel_ReturnsExpectedColor(
        string level,
        string expectedHex
    )
    {
        IBrush brush = Convert(converter: LevelColorConverter.Instance, level: level);

        brush.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)brush).Color.Should().Be(expected: Color.Parse(s: expectedHex));
    }

    [Theory]
    [InlineData(data: "information")]
    [InlineData(data: "unknown-level")]
    [InlineData(data: null)]
    public void LevelColorConverter_UnknownOrNullLevel_FallsBackToNeutralGray(string? level)
    {
        IBrush brush = Convert(converter: LevelColorConverter.Instance, level: level);

        ((SolidColorBrush)brush).Color.Should().Be(expected: Color.Parse(s: "#D1D5DB"));
    }

    [Theory]
    [InlineData(data: ["fatal", FontWeight.Bold])]
    [InlineData(data: ["error", FontWeight.Bold])]
    [InlineData(data: ["ERROR", FontWeight.Bold])]
    [InlineData(data: ["warning", FontWeight.Normal])]
    [InlineData(data: ["information", FontWeight.Normal])]
    [InlineData(data: [null, FontWeight.Normal])]
    public void LevelWeightConverter_ReturnsBoldOnlyForFatalOrError(
        string? level,
        FontWeight expected
    )
    {
        FontWeight weight = (FontWeight)
            LevelWeightConverter.Instance.Convert(
                value: level,
                targetType: typeof(FontWeight),
                parameter: null,
                culture: CultureInfo.InvariantCulture
            )!;

        weight.Should().Be(expected: expected);
    }

    [Fact]
    public void LogColorConverter_ValidHex_ParsesToMatchingBrush()
    {
        IBrush brush = Convert(converter: LogColorConverter.Instance, level: "#22C55E");

        ((SolidColorBrush)brush).Color.Should().Be(expected: Color.Parse(s: "#22C55E"));
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    public void LogColorConverter_NullOrEmpty_ReturnsDefaultBrush(string? colorHex)
    {
        IBrush brush = Convert(converter: LogColorConverter.Instance, level: colorHex);

        ((SolidColorBrush)brush).Color.Should().Be(expected: Color.Parse(s: "#D1D5DB"));
    }

    [Fact]
    public void LogColorConverter_UnparsableColor_FallsBackToDefaultBrushInsteadOfThrowing()
    {
        IBrush brush = Convert(converter: LogColorConverter.Instance, level: "not-a-color");

        ((SolidColorBrush)brush).Color.Should().Be(expected: Color.Parse(s: "#D1D5DB"));
    }
}
