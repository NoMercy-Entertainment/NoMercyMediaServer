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

namespace NoMercy.Encoder.Profiles;

public record LadderTier(
    int Width,
    int Height,
    string Label,
    int? RecommendedBitrateH264Kbps,
    int? RecommendedBitrateHevcKbps,
    int? RecommendedBitrateAv1Kbps,
    int? RecommendedBitrateVp9Kbps = null
);
