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

namespace NoMercy.Providers.FanArt.Models;

public class Albums
{
    private Image[]? _cover = [];
    private Image[]? _cdart = [];

    [JsonProperty("albumcover")]
    public Image[] Cover
    {
        get => _cover ?? [];
        set => _cover = value;
    }

    [JsonProperty("cdart")]
    public Image[] CdArt
    {
        get => _cdart ?? [];
        set => _cdart = value;
    }
}
