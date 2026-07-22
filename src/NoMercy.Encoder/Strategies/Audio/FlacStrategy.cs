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
/// FLAC single-file output. Lossless archival format for music collectors.
/// </summary>
public class FlacStrategy(IEncoder encoder, ILogger<FlacStrategy> logger, IStorage storage)
    : SinglePassStrategyBase(encoder: encoder, logger: logger, storage: storage)
{
    public override OutputFormat Format => OutputFormat.Flac;
}
