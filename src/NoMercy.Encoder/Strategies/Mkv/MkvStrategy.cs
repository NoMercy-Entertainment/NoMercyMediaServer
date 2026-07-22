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

namespace NoMercy.Encoder.Strategies.Mkv;

/// <summary>
/// Matroska single-file output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.MkvOutputStrategy"/> already emits the
/// correct FFmpeg arguments for a single .mkv output with embedded audio +
/// subtitles + chapters.
///
/// MKV does not distinguish single-pass vs two-pass conceptually (the output
/// is one file either way), but 2-pass can still matter for software
/// encoders targeting a specific bitrate. That's a future strategy.
/// </summary>
public class MkvStrategy(IEncoder encoder, ILogger<MkvStrategy> logger, IStorage storage)
    : SinglePassStrategyBase(encoder: encoder, logger: logger, storage: storage)
{
    public override OutputFormat Format => OutputFormat.Mkv;
}
