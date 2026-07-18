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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Orchestration;

/// <summary>
/// Top-level entry point for an encode job. Resolves the strategy matching
/// the request's format + encode mode and hands the encode off to it.
/// This is what queue jobs call instead of <see cref="IEncoder"/> directly —
/// keeps dispatch logic out of the job class.
/// </summary>
public interface IEncodingOrchestrator
{
    /// <summary>
    /// Run the full encode for <paramref name="request"/>. Used for
    /// strategies that return a single <see cref="EncodeTaskKind.Whole"/>
    /// task and by the coordinator when it finalizes after all children complete.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Run one decomposed task from a prior <c>Decompose</c> call.
    /// The request carries the full <see cref="EncodingRequest"/> (input path,
    /// profile, storage refs). The <paramref name="task"/> narrows execution to
    /// a single output slice identified by its <see cref="DecomposedTask.Kind"/>
    /// and <see cref="DecomposedTask.OutputIndex"/>.
    ///
    /// Used by <c>EncodeTaskJob</c> — one queue job per decomposed task.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        DecomposedTask task,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Run the pipeline through PlanStage only (Analyze → Validate → Plan),
    /// then call <c>strategy.Decompose(plan, groupTag)</c> to get the list of
    /// child tasks. No ffmpeg process is launched.
    ///
    /// Used by the encode coordinator before enqueuing child jobs so it knows
    /// how many tasks to expect and which queue names to route them to.
    /// Returns a single <see cref="EncodeTaskKind.Whole"/> task when the
    /// resolved strategy does not override <c>Decompose</c>.
    /// </summary>
    Task<DecomposedTask[]> DecomposeAsync(
        EncodingRequest request,
        string groupTag,
        CancellationToken ct = default
    );

    /// <summary>
    /// Plans every request in <paramref name="requests"/> independently — one
    /// <c>PlanAsync</c> call per preset, so each keeps its own hardware
    /// preference / HDR handling / codec resolution — then unions the results
    /// into ONE <see cref="OutputPlan"/> via <see cref="OutputPlanMerger"/>.
    /// <paramref name="requests"/>[0] is the PRIMARY request: its
    /// source-derived plan fields (subtitles, thumbnails, chapters, layout, …)
    /// win in the merge.
    ///
    /// Used by the smart-orchestrator merge path (<see cref="DecomposeMergedAsync"/>)
    /// and by the encode coordinator's FinalizeOnly pass so a merged run's
    /// master playlist is rebuilt from the SAME merged plan the children
    /// encoded against, without re-deriving it from just one preset's profile.
    ///
    /// Returns null when any request fails to plan, or when the plans don't
    /// share a compatible <see cref="OutputPlan.Format"/> — callers fall back
    /// to per-preset handling rather than merging an inconsistent set.
    /// </summary>
    Task<OutputPlan?> PlanMergedAsync(
        IReadOnlyList<EncodingRequest> requests,
        CancellationToken ct = default
    );

    /// <summary>
    /// The multi-preset counterpart to <see cref="DecomposeAsync"/>: builds
    /// ONE merged <see cref="OutputPlan"/> via <see cref="PlanMergedAsync"/>
    /// (union of every preset's video renditions, one shared audio/subtitle/
    /// thumbnail/chapter set) then calls <c>strategy.Decompose</c> once — a
    /// single coordinated encode instead of one independent encode per preset.
    ///
    /// A single-element <paramref name="requests"/> list degrades to exactly
    /// <see cref="DecomposeAsync"/> — "merge of one" is identical to today.
    ///
    /// Throws <see cref="MergedEncodingIncompatibleException"/> when the
    /// requests cannot be coordinated (different containers/formats, a plan
    /// failure, or no strategy registered for the resolved format/mode) —
    /// callers fall back to dispatching each preset independently.
    /// </summary>
    Task<DecomposedTask[]> DecomposeMergedAsync(
        IReadOnlyList<EncodingRequest> requests,
        string groupTag,
        CancellationToken ct = default
    );
}
