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

namespace NoMercy.Encoder.Strategies.Mp4;

/// <summary>
/// MP4 single-file single-pass output. Delegates to the shared pipeline — the
/// <see cref="NoMercy.Encoder.Output.Mp4OutputStrategy"/> already emits a single
/// faststart-muxed .mp4 with video + audio. Subtitles become sidecar files
/// because MP4's in-container subtitle support is limited.
/// </summary>
public class Mp4SinglePassStrategy(
    IEncoder encoder,
    ILogger<Mp4SinglePassStrategy> logger,
    IStorage storage
) : SinglePassStrategyBase(encoder, logger, storage)
{
    public override OutputFormat Format => OutputFormat.Mp4;
}
