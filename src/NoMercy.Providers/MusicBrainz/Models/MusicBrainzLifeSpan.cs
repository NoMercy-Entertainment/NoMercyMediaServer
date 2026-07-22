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

public class MusicBrainzLifeSpan
{
    // ReSharper disable once InconsistentNaming
    [JsonProperty(propertyName: "begin")]
    private string? _beginSpan { get; set; }

    public DateTime? BeginDate
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _beginSpan)
            && !string.IsNullOrEmpty(value: _beginSpan)
            && _beginSpan.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _beginSpan = value.ToString();
    }

    // ReSharper disable once InconsistentNaming
    [JsonProperty(propertyName: "end")]
    private string? _endSpan { get; set; }

    public DateTime? EndDate
    {
        get =>
            !string.IsNullOrWhiteSpace(value: _endSpan)
            && !string.IsNullOrEmpty(value: _endSpan)
            && _endSpan.TryParseToDateTime(dateTime: out DateTime dt)
                ? dt
                : null;
        set => _endSpan = value.ToString();
    }

    [JsonProperty(propertyName: "ended")]
    public bool Ended { get; set; }
}
