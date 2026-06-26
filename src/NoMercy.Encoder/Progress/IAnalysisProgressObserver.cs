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
/// Receives progress notifications from long-running analysis jobs
/// (crop detection, audio fingerprinting, Whisper transcription, subtitle
/// OCR, hardware benchmark). Implementations push the events wherever the
/// host needs them — SignalR for the dashboard, a capture list for tests,
/// or the null object for headless workers.
/// </summary>
public interface IAnalysisProgressObserver
{
    /// <summary>
    /// Called at sensible intervals while an analysis job is running.
    /// </summary>
    /// <param name="jobId">Stable identifier for this analysis run.</param>
    /// <param name="type">
    ///   Job category: <c>crop</c>, <c>fingerprint</c>, <c>whisper</c>,
    ///   <c>ocr</c>, or <c>benchmark</c>.
    /// </param>
    /// <param name="percent">Completion [0–100].</param>
    /// <param name="stage">Human-readable current stage label.</param>
    /// <param name="etaSeconds">Estimated seconds remaining, or null when unknown.</param>
    void Report(string jobId, string type, double percent, string stage, double? etaSeconds = null);
}
