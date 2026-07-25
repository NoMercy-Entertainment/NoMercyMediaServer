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

namespace NoMercy.Data.Requests;

public class LibrarySortRequest
{
    [JsonProperty("libraries")]
    public LibrarySortRequestItem[] Libraries { get; set; } = [];
}

public class LibrarySortRequestItem
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("order")]
    public int Order { get; set; }
}
