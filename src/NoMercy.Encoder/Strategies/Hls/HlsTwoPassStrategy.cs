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
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies.Hls;

/// <summary>
/// HLS 2-pass strategy. Pass 1 performs video-only analysis to a stats file,
/// pass 2 produces the final HLS output using those stats. See
/// <see cref="TwoPassStrategyBase"/> for the shared orchestration + checkpoint
/// resume logic.
/// </summary>
public class HlsTwoPassStrategy(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger<HlsTwoPassStrategy> logger,
    IStorage storage
) : TwoPassStrategyBase(encoder: encoder, checkpointStore: checkpointStore, logger: logger, storage: storage)
{
    public override OutputFormat Format => OutputFormat.Hls;
}
