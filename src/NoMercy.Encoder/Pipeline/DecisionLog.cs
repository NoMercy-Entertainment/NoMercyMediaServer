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

namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// One entry in the decisions log — a structured record of a choice
/// the encoder made during pipeline execution. The dashboard renders
/// the log per-job so users (and us, when debugging) can see exactly
/// why a profile picked NVENC over libx264, why a Dolby Vision file
/// got tonemapped to SDR, why one audio track was skipped, etc.
///
/// <para>Effortless principle: decisions ride alongside the encode
/// without ever blocking it. The user reads them later if curious;
/// the encoder doesn't ask permission to make them.</para>
/// </summary>
/// <param name="Stage">The pipeline stage that emitted this decision (e.g. <c>analyze</c>, <c>plan</c>, <c>build</c>).</param>
/// <param name="Key">Stable, dashboard-deep-linkable identifier for the decision class (e.g. <c>plan.encoder_resolved</c>, <c>analyze.dv_present</c>).</param>
/// <param name="Message">Human-readable description of the choice and why.</param>
/// <param name="Data">Optional structured payload for the dashboard / tooling — anything JSON-serialisable.</param>
public sealed record DecisionLog(string Stage, string Key, string Message, object? Data = null);
