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

namespace NoMercy.Database;

public class ColorPalette
{
    [JsonProperty(propertyName: "poster", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Poster { get; set; }

    [JsonProperty(propertyName: "backdrop", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Backdrop { get; set; }

    [JsonProperty(propertyName: "still", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Still { get; set; }

    [JsonProperty(propertyName: "profile", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Profile { get; set; }

    [JsonProperty(propertyName: "image", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Image { get; set; }

    [JsonProperty(propertyName: "cover", NullValueHandling = NullValueHandling.Ignore)]
    public PaletteColors? Cover { get; set; }

    /// <summary>
    /// Forgiving deserializer for the persisted color-palette JSON. Returns
    /// null for empty/missing/malformed strings so a corrupted DB row never
    /// takes a card render down. Use this everywhere instead of calling
    /// <see cref="JsonConvert.DeserializeObject{T}(string)"/> directly.
    /// </summary>
    public static ColorPalette? FromJsonOrNull(string? json)
    {
        if (string.IsNullOrEmpty(value: json))
            return null;
        try
        {
            return JsonConvert.DeserializeObject<ColorPalette>(value: json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
