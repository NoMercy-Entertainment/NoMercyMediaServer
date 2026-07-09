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

namespace NoMercy.Api.DTOs.Playlists;

/// <summary>POST /api/v1/playlists request body.</summary>
public record CreateUserPlaylistRequestDto
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("cover")]
    public string? Cover { get; set; }
}

/// <summary>
/// PATCH /api/v1/playlists/{id} request body. Every field is optional — only
/// the ones present are applied (partial update).
/// </summary>
public record UpdateUserPlaylistRequestDto
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("cover")]
    public string? Cover { get; set; }
}

/// <summary>POST /api/v1/playlists/{id}/items request body.</summary>
public record AddPlaylistItemRequestDto
{
    [JsonProperty("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonProperty("media_id")]
    public string MediaId { get; set; } = string.Empty;

    [JsonProperty("order")]
    public int? Order { get; set; }
}

/// <summary>PUT /api/v1/playlists/{id}/items/order request body.</summary>
public record ReorderPlaylistItemsRequestDto
{
    [JsonProperty("ordered_item_ids")]
    public List<string> OrderedItemIds { get; set; } = [];
}
