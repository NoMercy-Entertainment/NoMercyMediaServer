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

using NoMercy.Database;

namespace NoMercy.Tests.Database;

// ColorPalette.FromJsonOrNull is the only real logic on this value object: a forgiving
// deserializer that must never let a corrupted DB row take a card render down. Every
// branch (null/empty input, malformed JSON, valid JSON) is asserted here.
public class ColorPaletteTests
{
    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    public void FromJsonOrNull_NullOrEmptyInput_ReturnsNull(string? json)
    {
        ColorPalette? result = ColorPalette.FromJsonOrNull(json: json);

        Assert.Null(@object: result);
    }

    [Fact]
    public void FromJsonOrNull_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        ColorPalette? result = ColorPalette.FromJsonOrNull(json: "{not valid json");

        Assert.Null(@object: result);
    }

    [Fact]
    public void FromJsonOrNull_ValidJson_DeserializesEveryPaletteSlot()
    {
        const string json = """
            {
                "poster": { "dominant": "#111111", "primary": "#222222" },
                "backdrop": { "dominant": "#333333" },
                "still": { "dominant": "#444444" },
                "profile": { "dominant": "#555555" },
                "image": { "dominant": "#666666" },
                "cover": { "dominant": "#777777" }
            }
            """;

        ColorPalette? result = ColorPalette.FromJsonOrNull(json: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "#111111", actual: result!.Poster!.Dominant);
        Assert.Equal(expected: "#222222", actual: result.Poster.Primary);
        Assert.Equal(expected: "#333333", actual: result.Backdrop!.Dominant);
        Assert.Equal(expected: "#444444", actual: result.Still!.Dominant);
        Assert.Equal(expected: "#555555", actual: result.Profile!.Dominant);
        Assert.Equal(expected: "#666666", actual: result.Image!.Dominant);
        Assert.Equal(expected: "#777777", actual: result.Cover!.Dominant);
    }

    [Fact]
    public void FromJsonOrNull_JsonMissingASlot_LeavesThatSlotNull()
    {
        const string json = """{ "poster": { "dominant": "#111111" } }""";

        ColorPalette? result = ColorPalette.FromJsonOrNull(json: json);

        Assert.NotNull(@object: result);
        Assert.NotNull(@object: result!.Poster);
        Assert.Null(@object: result.Backdrop);
    }
}
