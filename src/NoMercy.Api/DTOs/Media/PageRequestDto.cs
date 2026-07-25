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
    private const int DefaultTake = 300;
    private const int MaxTake = 1000;

    private int _page;
    private int _take = DefaultTake;

    [FromQuery(Name = "page")]
    public int Page
    {
        get => _page;
        set => _page = value < 0 ? 0 : value;
    }

    // Clamp so a caller can't force an unbounded materialization with e.g.
    // ?take=500000. MaxTake is generous relative to the 300 default, so normal
    // paging is unaffected; a non-positive take falls back to the default.
    [FromQuery(Name = "take")]
    public int Take
    {
        get => _take;
        set => _take = value < 1 ? DefaultTake : Math.Min(value, MaxTake);
    }

    [FromQuery(Name = "version")]
    public string? Version { get; set; }
}
