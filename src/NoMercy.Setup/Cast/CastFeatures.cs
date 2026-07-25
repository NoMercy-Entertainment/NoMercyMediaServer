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

namespace NoMercy.Setup.Cast;

/// <summary>
/// Optional dev-only feature flags carried in <see cref="LaunchCustomData"/>.
/// </summary>
public class CastFeatures
{
    [JsonProperty("debug", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Debug { get; set; }

    [JsonProperty("skip_auth", NullValueHandling = NullValueHandling.Ignore)]
    public bool? SkipAuth { get; set; }
}
