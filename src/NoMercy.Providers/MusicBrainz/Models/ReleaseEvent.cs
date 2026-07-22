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
    [JsonProperty(propertyName: "area")]
    public MusicBrainzArea MusicBrainzArea { get; set; } = new();

    // ReSharper disable once InconsistentNaming
    [JsonProperty(propertyName: "date")]
    private string _date { get; set; } = string.Empty;

    [JsonProperty(propertyName: "dateTime")]
    public DateTime? DateTime
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _date)
            && !string.IsNullOrEmpty(value: _date)
            && _date.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _date = value.ToString().OrEmpty();
    }
}
