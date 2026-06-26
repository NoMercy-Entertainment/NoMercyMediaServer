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

namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Selects the correct <see cref="IQualityScaler"/> for a given FFmpeg
/// encoder handle. Walks the registered scalers in registration order,
/// returning the first whose <see cref="IQualityScaler.Supports"/> returns
/// true. Falls back to <see cref="LinearQualityScaler"/> when nothing else
/// matches.
/// </summary>
public interface IQualityScalerResolver
{
    IQualityScaler For(string encoderHandle);
}
