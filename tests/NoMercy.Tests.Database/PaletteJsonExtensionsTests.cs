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

using Newtonsoft.Json.Linq;
using NoMercy.Database;

namespace NoMercy.Tests.Database;

// ToRaw is the pass-through-without-round-trip path for the persisted palette JSON
// string. Every branch matters: null/empty/"{}" must all collapse to null (same
// forgiveness as ColorPalette.FromJsonOrNull), a malformed string must never throw, and
// a valid string must come back as the equivalent parsed token, not the original object.
public class PaletteJsonExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    public void ToRaw_NullEmptyOrEmptyObject_ReturnsNull(string? json)
    {
        JToken? result = json.ToRaw();

        Assert.Null(result);
    }

    [Fact]
    public void ToRaw_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        JToken? result = "{not valid json".ToRaw();

        Assert.Null(result);
    }

    [Fact]
    public void ToRaw_ValidJson_ReturnsTheEquivalentParsedToken()
    {
        const string json = """{ "poster": { "dominant": "#111111" } }""";

        JToken? result = json.ToRaw();

        Assert.NotNull(result);
        Assert.Equal("#111111", result!["poster"]!["dominant"]!.Value<string>());
    }
}
