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

public class AnimeInfo
{
    public string FileName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? Title { get; set; }
    public string? ExtraInfo { get; set; }
    public string? Checksum { get; set; }
    public string? Extension { get; set; }
}
