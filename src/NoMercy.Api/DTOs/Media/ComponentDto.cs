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
using NoMercy.Api.DTOs.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record Render
{
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "data")]
    public IEnumerable<object> Data { get; set; } = [];
}

public record ComponentDto<T>
{
    public ComponentDto()
    {
        Id = Ulid.NewUlid();
        Update = new(ulid: Id) { When = null };
    }

    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "component")]
    public string Component { get; set; } = string.Empty;

    [JsonProperty(propertyName: "props")]
    public RenderProps<T> Props { get; set; } = new();

    [JsonProperty(propertyName: "update")]
    public Update Update { get; set; }

    [JsonProperty(propertyName: "replacing")]
    public Ulid Replacing { get; set; }
}

public record Update
{
    [JsonProperty(propertyName: "when")]
    public string? When { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = default!;

    [JsonProperty(propertyName: "body")]
    public object Body { get; set; } = new();

    public Update(Ulid ulid)
    {
        Body = new { replace_id = ulid };
    }
}

public record RenderProps<T>
{
    [JsonProperty(propertyName: "id")]
    public dynamic Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "next_id")]
    public dynamic NextId { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "previous_id")]
    public dynamic PreviousId { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "more_link")]
    public Uri? MoreLink { get; set; }

    [JsonProperty(propertyName: "more_link_text")]
    public string? MoreText => MoreLink is not null ? "See all".Localize() : null;

    [JsonProperty(propertyName: "items")]
    public IEnumerable<ComponentDto<T>>? Items { get; set; } = [];

    [JsonProperty(propertyName: "data")]
    public T? Data { get; set; }

    [JsonProperty(propertyName: "watch")]
    public bool Watch { get; set; }

    [JsonProperty(propertyName: "context_menu_items")]
    public Dictionary<string, object>[]? ContextMenuItems { get; set; } = [];

    [JsonProperty(propertyName: "url")]
    public Uri? Url { get; set; }

    [JsonProperty(propertyName: "displayList")]
    public IEnumerable<ArtistTrackDto>? DisplayList { get; set; } = [];

    [JsonProperty(propertyName: "properties")]
    public Dictionary<string, dynamic> Properties { get; set; } = new();
}
