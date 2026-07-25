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
        (IBrush)converter.Convert(level, typeof(IBrush), null, CultureInfo.InvariantCulture)!;

    [Theory]
    [InlineData(["fatal", "#DC2626"])]
    [InlineData(["FATAL", "#DC2626"])]
    [InlineData(["error", "#EF4444"])]
    [InlineData(["warning", "#EAB308"])]
    [InlineData(["debug", "#6B7280"])]
    [InlineData(["verbose", "#4B5563"])]
    public void LevelColorConverter_KnownLevel_ReturnsExpectedColor(
        string level,
        string expectedHex
    )
    {
        IBrush brush = Convert(LevelColorConverter.Instance, level);

        brush.Should().BeOfType<SolidColorBrush>();
        ((SolidColorBrush)brush).Color.Should().Be(Color.Parse(expectedHex));
    }

    [Theory]
    [InlineData("information")]
    [InlineData("unknown-level")]
    [InlineData(null)]
    public void LevelColorConverter_UnknownOrNullLevel_FallsBackToNeutralGray(string? level)
    {
        IBrush brush = Convert(LevelColorConverter.Instance, level);

        ((SolidColorBrush)brush).Color.Should().Be(Color.Parse("#D1D5DB"));
    }

    [Theory]
    [InlineData(["fatal", FontWeight.Bold])]
    [InlineData(["error", FontWeight.Bold])]
    [InlineData(["ERROR", FontWeight.Bold])]
    [InlineData(["warning", FontWeight.Normal])]
    [InlineData(["information", FontWeight.Normal])]
    [InlineData([null, FontWeight.Normal])]
    public void LevelWeightConverter_ReturnsBoldOnlyForFatalOrError(
        string? level,
        FontWeight expected
    )
    {
        FontWeight weight = (FontWeight)
            LevelWeightConverter.Instance.Convert(
                level,
                typeof(FontWeight),
                null,
                CultureInfo.InvariantCulture
            )!;

        weight.Should().Be(expected);
    }

    [Fact]
    public void LogColorConverter_ValidHex_ParsesToMatchingBrush()
    {
        IBrush brush = Convert(LogColorConverter.Instance, "#22C55E");

        ((SolidColorBrush)brush).Color.Should().Be(Color.Parse("#22C55E"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LogColorConverter_NullOrEmpty_ReturnsDefaultBrush(string? colorHex)
    {
        IBrush brush = Convert(LogColorConverter.Instance, colorHex);

        ((SolidColorBrush)brush).Color.Should().Be(Color.Parse("#D1D5DB"));
    }

    [Fact]
    public void LogColorConverter_UnparsableColor_FallsBackToDefaultBrushInsteadOfThrowing()
    {
        IBrush brush = Convert(LogColorConverter.Instance, "not-a-color");

        ((SolidColorBrush)brush).Color.Should().Be(Color.Parse("#D1D5DB"));
    }
}
