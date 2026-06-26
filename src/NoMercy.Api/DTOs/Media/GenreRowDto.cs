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

using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record GenreRowDto<T>
{
    [JsonProperty("id")]
    public dynamic Id { get; set; } = string.Empty;

    [JsonProperty("next_id")]
    public dynamic NextId { get; set; } = Ulid.NewUlid();

    [JsonProperty("previous_id")]
    public dynamic PreviousId { get; set; } = Ulid.NewUlid();

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("more_link")]
    public Uri? MoreLink { get; set; }

    [JsonProperty("items")]
    public IEnumerable<T?> Items { get; set; } = [];

    [NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonProperty("source")]
    public IEnumerable<HomeSourceDto> Source { get; set; } = [];
}
