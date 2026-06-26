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

namespace NoMercy.Service.Seeds.Data;

public static class EncoderProfileSeedData
{
    public record SeedExample(string Name, string ParentBuiltinName);

    public static readonly SeedExample[] Examples =
    [
        new("Example: Web 1080p", "Web 1080p Balanced"),
        new("Example: Anime 1080p", "Anime HEVC 1080p 10-bit"),
        new("Example: Music FLAC", "Music FLAC Lossless"),
    ];
}
