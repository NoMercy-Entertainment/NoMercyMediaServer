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

namespace NoMercy.Plugins.Abstractions;

public interface IEncoderPlugin : IPlugin
{
    // Return a profile to override the configured one for this source, or null to
    // opt out (the configured profile stands). The first plugin returning non-null
    // wins.
    EncodingProfile? GetProfile(MediaInfo info);
}
