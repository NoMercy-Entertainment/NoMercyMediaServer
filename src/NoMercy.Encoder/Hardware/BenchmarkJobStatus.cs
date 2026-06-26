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

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Snapshot of a single benchmark job's lifecycle.
/// Codecs and Resolutions record what the caller requested; the underlying
/// <see cref="IHardwareBenchmark.CalibrateAsync"/> always runs all codecs
/// (codec/resolution filtering is a forward-looking feature not yet wired
/// into the benchmark engine).
/// </summary>
public sealed record BenchmarkJobStatus(
    string JobId,
    string Status, // "queued" | "running" | "completed" | "failed" | "cancelled"
    DateTime StartedAt,
    DateTime? CompletedAt,
    int MeasurementCount, // 0 until completed
    IReadOnlyList<string> RequestedCodecs,
    IReadOnlyList<int> RequestedResolutions,
    string? Error // non-null when failed
);
