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

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Execution;

public interface IFfmpegExecutor
{
    Task<ExecutionResult> ExecuteAsync(
        FfmpegCommand command,
        TimeSpan inputDuration,
        Action<EncodingProgress>? onProgress = null,
        string? correlationId = null,
        CancellationToken ct = default
    );
}
