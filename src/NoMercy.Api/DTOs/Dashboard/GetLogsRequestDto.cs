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

namespace NoMercy.Api.DTOs.Dashboard;

public record GetLogsRequestDto
{
    [FromQuery(Name = "limit")]
    public int Limit { get; init; } = 50;

    [FromQuery(Name = "types")]
    public string[]? Types { get; init; }

    [FromQuery(Name = "levels")]
    public string[]? Levels { get; init; }

    [FromQuery(Name = "filter")]
    public string? Filter { get; init; }
}
