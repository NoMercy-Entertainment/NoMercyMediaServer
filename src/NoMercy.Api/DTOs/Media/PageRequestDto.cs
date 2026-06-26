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

using Microsoft.AspNetCore.Mvc;

namespace NoMercy.Api.DTOs.Media;

public class PageRequestDto
{
    [FromQuery(Name = "page")]
    public int Page { get; set; }

    [FromQuery(Name = "take")]
    public int Take { get; set; } = 300;

    [FromQuery(Name = "version")]
    public string? Version { get; set; }
}
