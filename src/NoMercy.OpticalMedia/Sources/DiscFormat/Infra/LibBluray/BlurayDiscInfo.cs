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

namespace NoMercy.DiscFormat.LibBluray;

/// Decoded information about an open Blu-ray disc.
public sealed record BlurayDiscInfo
{
    public required bool BlurayDetected { get; init; }
    public required bool AacsDetected { get; init; }
    public required bool AacsHandled { get; init; }
    public required bool BdplusDetected { get; init; }
    public required bool FirstPlaySupported { get; init; }
    public required bool TopMenuSupported { get; init; }
    public required uint NumTitles { get; init; }
    public required uint NumHdmvTitles { get; init; }
    public required uint NumBdjTitles { get; init; }
    public required uint NumUnsupportedTitles { get; init; }
    public required int AacsErrorCode { get; init; }
    public required int AacsMkbv { get; init; }
    public string? DiscName { get; init; }
    public string? UdfVolumeId { get; init; }

    /// The 20-byte AACS disc id (the disc's cryptographic identity), lowercase hex. Empty when the
    /// disc is unprotected or AACS was not handled. This is the strongest disc fingerprint source.
    public required string DiscIdHex { get; init; }
}
