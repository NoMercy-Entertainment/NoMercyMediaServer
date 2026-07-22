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
using NoMercy.Data.Repositories;

namespace NoMercy.Api.DTOs.Playlists;

/// <summary>
/// One of the caller's user-created, video-only playlists, as a summary card
/// (GET /api/v1/playlists and the POST /api/v1/playlists create response).
/// </summary>
public record UserPlaylistSummaryDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "item_count")]
    public int ItemCount { get; set; }

    public UserPlaylistSummaryDto() { }

    public UserPlaylistSummaryDto(UserPlaylistSummary summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        Cover = summary.Cover;
        ItemCount = summary.ItemCount;
    }
}
