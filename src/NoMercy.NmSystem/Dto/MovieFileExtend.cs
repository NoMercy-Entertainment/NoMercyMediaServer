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

namespace NoMercy.NmSystem.Dto;

public class MovieFileExtend
{
    public string? Title { get; init; }
    public string? Year { get; init; }
    public bool IsSeries { get; set; }
    public int? Season { get; init; }
    public int? Episode { get; init; }
    public bool IsSuccess { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public int DiscNumber { get; set; }
    public int TrackNumber { get; set; }
}
