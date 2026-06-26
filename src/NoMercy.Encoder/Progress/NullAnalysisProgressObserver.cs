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

/// <summary>
/// No-op implementation of <see cref="IAnalysisProgressObserver"/>.
/// Used as the DI default so analysis jobs compile and run without a
/// real SignalR hub — tests and headless workers use this automatically.
/// </summary>
public sealed class NullAnalysisProgressObserver : IAnalysisProgressObserver
{
    public static readonly NullAnalysisProgressObserver Instance = new();

    public void Report(
        string jobId,
        string type,
        double percent,
        string stage,
        double? etaSeconds = null
    ) { }
}
