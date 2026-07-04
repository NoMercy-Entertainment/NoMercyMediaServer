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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Database;

public class ColorPaletteTimeStamps : Timestamps
{
    [Column("ColorPalette")]
    [StringLength(1024)]
    [JsonProperty("color_palette")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _colorPalette { get; set; } = "";

    [NotMapped]
    public ColorPalette? ColorPalette
    {
        get => ColorPalette.FromJsonOrNull(_colorPalette);
        set => _colorPalette = JsonConvert.SerializeObject(value);
    }
}
