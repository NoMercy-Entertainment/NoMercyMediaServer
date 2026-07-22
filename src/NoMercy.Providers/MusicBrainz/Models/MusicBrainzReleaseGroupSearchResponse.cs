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

namespace NoMercy.Providers.MusicBrainz.Models;

public class MusicBrainzReleaseGroupSearchResponse
{
    [JsonProperty(propertyName: "created")]
    public DateTimeOffset Created { get; set; }

    [JsonProperty(propertyName: "count")]
    public long Count { get; set; }

    [JsonProperty(propertyName: "offset")]
    public long Offset { get; set; }

    [JsonProperty(propertyName: "release-groups")]
    public MusicBrainzReleaseGroup[] ReleaseGroups { get; set; } = [];
}
