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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Lets a layer above the encoder replace the configured profile based on the
/// analyzed source — the seam that wires <c>IEncoderPlugin.GetProfile</c> without
/// the encoder taking a dependency on the plugin system (which depends on the
/// encoder). Applied once, after Analyze and before Validate. When no
/// implementation is registered the encoder uses the configured profile unchanged.
/// </summary>
public interface IProfileOverride
{
    EncodingProfile Apply(EncodingProfile configured, MediaInfo media);
}
