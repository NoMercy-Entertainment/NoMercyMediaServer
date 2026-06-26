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

using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Execution;

public record ExecutionResult(
    bool Success,
    int ExitCode,
    string StdErr,
    TimeSpan Duration,
    EncodingError? Error,
    ExecutionMetrics? Metrics = null
);

public record ExecutionMetrics(
    double AverageSpeed,
    double AverageFps,
    double PeakSpeed,
    double PeakFps,
    long TotalSizeBytes
);
