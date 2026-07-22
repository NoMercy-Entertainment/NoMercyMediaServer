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

using System.Drawing;
using NoMercy.NmSystem.Extensions;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.Tests.NmSystem;

[Trait(name: "Category", value: "Unit")]
public class ColorExtensionsTests
{
    [Theory]
    [InlineData(data: [0, 0, 0, "#000000"])]
    [InlineData(data: [255, 255, 255, "#FFFFFF"])]
    [InlineData(data: [255, 0, 0, "#FF0000"])]
    [InlineData(data: [0, 255, 0, "#00FF00"])]
    [InlineData(data: [0, 0, 255, "#0000FF"])]
    [InlineData(data: [128, 64, 192, "#8040C0"])]
    public void ToHexString_ConvertsSdColorToHex(int red, int green, int blue, string expected)
    {
        Color color = Color.FromArgb(red: red, green: green, blue: blue);
        string result = color.ToHexString();
        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [0, 0, 0, "#000000"])]
    [InlineData(data: [255, 255, 255, "#FFFFFF"])]
    [InlineData(data: [255, 0, 0, "#FF0000"])]
    [InlineData(data: [0, 255, 0, "#00FF00"])]
    [InlineData(data: [0, 0, 255, "#0000FF"])]
    [InlineData(data: [128, 64, 192, "#8040C0"])]
    public void ToHexString_ConvertsImageSharpRgb24ToHex(
        byte red,
        byte green,
        byte blue,
        string expected
    )
    {
        Rgb24 color = new(r: red, g: green, b: blue);
        string result = color.ToHexString();
        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ToHexString_ProducesUppercaseHexDigits()
    {
        Color color = Color.FromArgb(red: 171, green: 205, blue: 239);
        string result = color.ToHexString();
        result.Should().Be(expected: "#ABCDEF");
        result.Should().StartWith(expected: "#");
        result.Length.Should().Be(expected: 7);
    }
}
