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

using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Jobs;

public record BatchEncodingRequest(EncodingRequest[] Items, BatchOptions Options);

public record BatchOptions(
    bool ShareAnalysis = true,
    bool ParallelEncoding = false,
    int MaxParallel = 1,
    BatchCancellationMode CancelMode = BatchCancellationMode.SkipRemaining
);

public enum BatchCancellationMode
{
    SkipRemaining,
    CancelAll,
    CancelAndClean,
}
