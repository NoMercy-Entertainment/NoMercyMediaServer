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

public class MediaFolder
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Accessed { get; set; }
    public string Type { get; set; } = string.Empty;
    public MovieFileExtend Parsed { get; init; } = new();
}
