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

namespace NoMercy.Encoder.Progress;

public record EncodingProgress(
    string CorrelationId,
    double PercentComplete,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining,
    double? CurrentFps,
    double? CurrentSpeed,
    string? CurrentStage,
    string? CurrentOperation,
    int? BitrateKbps = null,
    string? Bitrate = null,
    int ProcessId = 0,
    double CurrentTimeSeconds = 0,
    double DurationSeconds = 0
);
