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

// ColorPalettes (the mixin base every color-palette-carrying entity extends) stores the
// palette as a JSON string but exposes it as a typed ColorPalette. The getter delegates
// to the already-100%-covered ColorPalette.FromJsonOrNull; what this test owns is that
// the setter actually serializes into the backing column and the getter reads it back.
public class ColorPalettesTests
{
    [Fact]
    public void ColorPalette_Set_SerializesIntoTheBackingColumn()
    {
        ColorPalettes entity = new()
        {
            ColorPalette = new ColorPalette { Poster = new() { Dominant = "#123456" } },
        };

        Assert.Contains(expectedSubstring: "#123456", actualString: entity._colorPalette);
    }

    [Fact]
    public void ColorPalette_Get_DeserializesTheBackingColumnBack()
    {
        ColorPalettes entity = new()
        {
            ColorPalette = new ColorPalette { Poster = new() { Dominant = "#123456" } },
        };

        ColorPalette? result = entity.ColorPalette;

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "#123456", actual: result!.Poster!.Dominant);
    }

    [Fact]
    public void ColorPalette_Get_EmptyBackingColumn_ReturnsNull()
    {
        ColorPalettes entity = new();

        Assert.Null(@object: entity.ColorPalette);
    }
}
