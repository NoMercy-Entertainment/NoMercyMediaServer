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

using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Startup;
using NoMercyQueue;

namespace NoMercy.Service.Configuration;

/// <summary>
/// Real <see cref="IEncoderActivityProbe"/> implementation that plugs
/// the encoder's deferred benchmark into the live queue and streaming
/// state. The encoder is considered busy when there's at least one
/// active live transcode session OR at least one queue worker actually
/// processing an encoder job. Workers that are spawned-but-idle (alive,
/// waiting on the next job) do NOT count — that's the difference
/// between <see cref="QueueRunner.CountWorkersProcessingJob"/> (this)
/// and <see cref="QueueRunner.GetActiveWorkerThreads"/> (every spawned
/// thread, idle or busy). Conflating the two made the benchmark defer
/// forever the moment any encoder worker thread existed.
/// </summary>
internal sealed class EncoderActivityProbe(QueueRunner queueRunner, ISessionManager sessionManager)
    : IEncoderActivityProbe
{
    public bool IsBusy
    {
        get
        {
            if (sessionManager.ActiveSessionCount > 0)
                return true;

            return queueRunner.CountWorkersProcessingJob(namePredicate: name =>
                    name.StartsWith(value: "encoder", comparisonType: StringComparison.OrdinalIgnoreCase)
                ) > 0;
        }
    }
}
