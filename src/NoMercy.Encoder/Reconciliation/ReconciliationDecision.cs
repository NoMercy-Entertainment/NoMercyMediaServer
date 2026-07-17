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

using NoMercy.Encoder.Decomposition;

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// The reconciler's verdict for one already-encoded file being re-dispatched.
/// </summary>
/// <param name="Action">Skip, Partial, or Full — see <see cref="ReconciliationAction"/>.</param>
/// <param name="MissingKinds">
/// For <see cref="ReconciliationAction.Partial"/>: which decomposition-level
/// kinds (Video/Audio/Subtitle/Thumbnails/Chapters) are missing or invalid
/// and must be re-run. Callers filter the strategy's <c>DecomposedTask[]</c>
/// down to these kinds before dispatching — the EXISTING TaskFilter /
/// DecomposedTask machinery drives the partial run, nothing new to execute
/// it. Empty for Skip and for Full (Full re-runs everything, unfiltered).
/// </param>
/// <param name="NeedsSubtitleOcr">
/// True when one or more bitmap subtitle streams on the source still need an
/// OCR sidecar. This is orthogonal to <see cref="MissingKinds"/> because
/// bitmap OCR is not a decomposed encode task — it is a lightweight
/// post-encode pass that never touches ffmpeg's Build/Execute stages.
/// </param>
public sealed record ReconciliationDecision(
    ReconciliationAction Action,
    IReadOnlyList<EncodeTaskKind> MissingKinds,
    bool NeedsSubtitleOcr,
    string Reason
)
{
    /// <summary>True when there is nothing at all for the pipeline to run —
    /// no decomposed task, no OCR pass. Only ever true for Skip.</summary>
    public bool IsNoOp => Action == ReconciliationAction.Skip;
}
