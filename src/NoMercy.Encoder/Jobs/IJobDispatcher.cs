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

using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Jobs;

public interface IJobDispatcher
{
    Task<RemoteEncodingResult> DispatchAsync(
        EncodingJob job,
        IProgress<EncodingProgress> progress,
        CancellationToken ct
    );

    IReadOnlyList<IRemoteWorker> AvailableWorkers { get; }

    IRemoteWorker? SelectWorker(EncodingJob job);
}
