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

namespace NoMercy.Encoder.Hdr;

public record HdrOptions(
    TonemapAlgorithm Algorithm = TonemapAlgorithm.Hable,
    string? CustomLutPath = null,
    LutApplication LutApply = LutApplication.AfterTonemap,
    double Desat = 0.0,
    double Peak = 0.0,
    bool PreserveMetadata = false
);

public enum TonemapAlgorithm
{
    Hable,
    Reinhard,
    Mobius,
    Bt2390,
}

public enum LutApplication
{
    BeforeTonemap,
    AfterTonemap,
    InsteadOfTonemap,
}
