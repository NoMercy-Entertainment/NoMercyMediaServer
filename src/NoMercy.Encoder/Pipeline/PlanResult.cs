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
/// Top-level spec-shape produced by <see cref="PlanResultProjector"/> and
/// consumed by the dashboard controller (Phase 1.7) and eventually
/// embedded in <c>EncodingResult</c> (Phase 1.9). It is a stable,
/// JSON-serialisable view of the encoder's plan — decoupled from the
/// internal <see cref="NoMercy.Encoder.Pipeline.Stages.ExecutionPlan"/>
/// so the API contract can evolve independently of the execution graph.
///
/// <para>The dashboard uses <see cref="Strategy"/> to label the job card
/// (e.g. "HLS · CRF"), <see cref="Variants"/> to render the variant
/// table, <see cref="Subtitles"/> for the subtitle track list,
/// <see cref="HardwareBindings"/> for the device badge, and
/// <see cref="DecisionsLog"/> for the collapsible decision trace.</para>
/// </summary>
public sealed record PlanResult(
    string Strategy,
    IReadOnlyList<VariantPlan> Variants,
    IReadOnlyList<SubtitlePlan> Subtitles,
    HardwareBindings HardwareBindings,
    IReadOnlyList<DecisionLog> DecisionsLog
);
