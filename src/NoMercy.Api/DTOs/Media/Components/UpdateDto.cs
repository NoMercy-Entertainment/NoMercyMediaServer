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

namespace NoMercy.Api.DTOs.Media.Components;

public record UpdateDto
{
    [JsonProperty("when", NullValueHandling = NullValueHandling.Ignore)]
    public string? When { get; set; }

    [JsonProperty("link", NullValueHandling = NullValueHandling.Ignore)]
    public Uri? Link { get; set; }

    [JsonProperty("body", NullValueHandling = NullValueHandling.Ignore)]
    public object? Body { get; set; }

    public UpdateDto() { }

    public UpdateDto(Ulid replaceId)
    {
        Body = new { replace_id = replaceId };
    }

    public static UpdateDto OnPageLoad(string link, Ulid? replaceId = null)
    {
        return new()
        {
            When = "pageLoad",
            Link = new(link, UriKind.Relative),
            Body = replaceId.HasValue ? new { replace_id = replaceId.Value } : null,
        };
    }

    public static UpdateDto OnInterval(string link, int intervalMs, Ulid? replaceId = null)
    {
        return new()
        {
            When = $"interval:{intervalMs}",
            Link = new(link, UriKind.Relative),
            Body = replaceId.HasValue ? new { replace_id = replaceId.Value } : null,
        };
    }
}
