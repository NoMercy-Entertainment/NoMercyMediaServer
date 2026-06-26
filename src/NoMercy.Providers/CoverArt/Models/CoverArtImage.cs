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
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.CoverArt.Models;

public class CoverArtImage
{
    // ReSharper disable once InconsistentNaming
    private readonly Uri? __image;

    [JsonProperty("approved")]
    public bool Approved { get; set; }

    [JsonProperty("back")]
    public bool Back { get; set; }

    [JsonProperty("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonProperty("edit")]
    public int Edit { get; set; }

    [JsonProperty("front")]
    public bool Front { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("image")]
    public Uri? Image
    {
        get => __image?.ToHttps();
        init => __image = value;
    }

    [JsonProperty("thumbnails")]
    public CoverArtThumbnails CoverArtThumbnails { get; set; } = new();

    [JsonProperty("types")]
    public string[] Types { get; set; } = [];
}
