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

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// One recognized BDAV stream coding type: its wire value, attribute layout kind, and the codec
/// name carried into the bundle's stream tables.
public sealed record CodingTypeInfo
{
    public required int Value { get; init; }
    public required CodingKind Kind { get; init; }
    public required string Name { get; init; }
}
