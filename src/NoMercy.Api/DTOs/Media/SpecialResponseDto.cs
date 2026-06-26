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
using Special = NoMercy.Database.Models.TvShows.Special;

namespace NoMercy.Api.DTOs.Media;

public record SpecialResponseDto
{
    [JsonProperty("nextId")]
    public object NextId { get; set; } = null!;

    [JsonProperty("data")]
    public SpecialResponseItemDto? Data { get; set; }

    public SpecialResponseDto(Special special)
    {
        //
    }

    public SpecialResponseDto()
    {
        //
    }
}
