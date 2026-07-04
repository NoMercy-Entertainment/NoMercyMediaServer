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
    [JsonProperty("tiny")]
    public Uri? Tiny { get; set; }

    [JsonProperty("large")]
    public Uri? Large { get; set; }

    [JsonProperty("small")]
    public Uri? Small { get; set; }

    [JsonProperty("medium")]
    public Uri? Medium { get; set; }

    [JsonProperty("original")]
    public Uri? Original { get; set; }

    [JsonProperty("meta")]
    public CoverImageMeta? Meta { get; set; }
}
