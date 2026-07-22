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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public sealed class InboxAssignRequest
{
    [JsonProperty(propertyName: "type")]
    public required string Type { get; set; }

    [JsonProperty(propertyName: "match")]
    public required CandidateMatch Match { get; set; }

    [JsonProperty(propertyName: "target_library_id")]
    public required Ulid TargetLibraryId { get; set; }

    [JsonProperty(propertyName: "target_folder_id")]
    public required Ulid TargetFolderId { get; set; }

    [JsonProperty(propertyName: "target_profile_id")]
    public required Ulid TargetProfileId { get; set; }
}
