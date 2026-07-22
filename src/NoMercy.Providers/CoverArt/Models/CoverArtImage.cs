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

    [JsonProperty(propertyName: "approved")]
    public bool Approved { get; set; }

    [JsonProperty(propertyName: "back")]
    public bool Back { get; set; }

    [JsonProperty(propertyName: "comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonProperty(propertyName: "edit")]
    public int Edit { get; set; }

    [JsonProperty(propertyName: "front")]
    public bool Front { get; set; }

    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty(propertyName: "image")]
    public Uri? Image
    {
        get => __image?.ToHttps();
        init => __image = value;
    }

    [JsonProperty(propertyName: "thumbnails")]
    public CoverArtThumbnails CoverArtThumbnails { get; set; } = new();

    [JsonProperty(propertyName: "types")]
    public string[] Types { get; set; } = [];
}
