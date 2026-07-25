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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Database;

public static class PaletteJsonExtensions
{
    /// <summary>
    /// The color palette is persisted as a JSON string and sent to clients as JSON.
    /// Materializing it into a typed <see cref="ColorPalette"/> (reflection-heavy
    /// deserialize) only to have the response serializer reflect it back to the same
    /// JSON is a pure round-trip. For read DTOs that just forward the palette, hand the
    /// serializer this parsed token instead: it validates the stored string (malformed →
    /// null, same forgiveness as <see cref="ColorPalette.FromJsonOrNull"/>) and is
    /// emitted verbatim, skipping the typed object entirely.
    /// </summary>
    public static JToken? ToRaw(this string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}")
            return null;
        try
        {
            return JToken.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
