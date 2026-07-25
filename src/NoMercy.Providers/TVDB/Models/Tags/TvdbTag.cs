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
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Tags;

public class TvdbTagOptionsResponse : TvdbResponse<TvdbTag[]> { }

public class TvdbTagOptionResponse : TvdbResponse<TvdbTag> { }

public class TvdbTag
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("allowsMultiple")]
    public bool AllowsMultiple { get; set; }

    [JsonProperty("helpText")]
    public string? HelpText { get; set; }

    [JsonProperty("options")]
    public TvdbTagOption[] Options { get; set; } = [];
}
