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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies.Audio;

/// <summary>
/// Ogg single-file output. Container accepts vorbis (default), opus, or flac
/// depending on profile's audio codec selection.
/// </summary>
public class OggStrategy(IEncoder encoder, ILogger<OggStrategy> logger, IStorage storage)
    : SinglePassStrategyBase(encoder, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Ogg;
}
