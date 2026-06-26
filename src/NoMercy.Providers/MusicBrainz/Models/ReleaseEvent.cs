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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Providers.MusicBrainz.Models;

public class ReleaseEvent
{
    [JsonProperty("area")]
    public MusicBrainzArea MusicBrainzArea { get; set; } = new();

    // ReSharper disable once InconsistentNaming
    [JsonProperty("date")]
    private string _date { get; set; } = string.Empty;

    [JsonProperty("dateTime")]
    public DateTime? DateTime
    {
        get =>
            !string.IsNullOrWhiteSpace(_date)
            && !string.IsNullOrEmpty(_date)
            && _date.TryParseToDateTime(out DateTime dt)
                ? dt
                : null;
        set => _date = value.ToString().OrEmpty();
    }
}
