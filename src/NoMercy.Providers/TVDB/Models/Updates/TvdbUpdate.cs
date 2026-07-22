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

namespace NoMercy.Providers.TVDB.Models.Updates;

public class TvdbUpdatesResponse : TvdbResponse<TvdbUpdate[]> { }

public class TvdbUpdate
{
    [JsonProperty(propertyName: "recordType")]
    public string RecordType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "recordId")]
    public long RecordId { get; set; }

    [JsonProperty(propertyName: "methodInt")]
    public int MethodInt { get; set; }

    [JsonProperty(propertyName: "method")]
    public string Method { get; set; } = string.Empty;

    [JsonProperty(propertyName: "extraInfo")]
    public string? ExtraInfo { get; set; }

    [JsonProperty(propertyName: "userId")]
    public long? UserId { get; set; }

    [JsonProperty(propertyName: "timeStamp")]
    public long TimeStamp { get; set; }

    [JsonProperty(propertyName: "entityType")]
    public string? EntityType { get; set; }

    [JsonProperty(propertyName: "mergeToId")]
    public long? MergeToId { get; set; }

    [JsonProperty(propertyName: "mergeToEntityType")]
    public string? MergeToEntityType { get; set; }
}
