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

namespace NoMercy.Providers.KitsuIo;

public class PosterImage
{
    [JsonProperty(propertyName: "tiny")]
    public Uri? Tiny { get; set; }

    [JsonProperty(propertyName: "large")]
    public Uri? Large { get; set; }

    [JsonProperty(propertyName: "small")]
    public Uri? Small { get; set; }

    [JsonProperty(propertyName: "medium")]
    public Uri? Medium { get; set; }

    [JsonProperty(propertyName: "original")]
    public Uri? Original { get; set; }

    [JsonProperty(propertyName: "meta")]
    public CoverImageMeta? Meta { get; set; }
}
