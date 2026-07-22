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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Strategies;

/// <summary>
/// Optional per-strategy stage overrides. Return null for a role to keep the
/// pipeline default; return a custom instance to replace that stage for this
/// strategy's encodes only. Default implementations return null for everything
/// so existing strategies require no changes.
/// </summary>
public interface IStageOverrides
{
    /// <summary>Gets a custom validation stage, or null to use the pipeline default.</summary>
    IValidationStage? Validation => null;

    /// <summary>Gets a custom analysis stage, or null to use the pipeline default.</summary>
    IAnalysisStage? Analysis => null;

    /// <summary>Gets a custom planning stage, or null to use the pipeline default.</summary>
    IPlanStage? Plan => null;

    /// <summary>Gets a custom build stage, or null to use the pipeline default.</summary>
    IBuildStage? Build => null;

    /// <summary>Gets a custom execution stage, or null to use the pipeline default.</summary>
    IExecutionStage? Execution => null;

    /// <summary>Gets a custom finalization stage, or null to use the pipeline default.</summary>
    IFinalizeStage? Finalize => null;
}

/// <summary>
/// Owns the full encode lifecycle for a single {OutputFormat, EncodeMode}
/// combination. The orchestrator resolves exactly one strategy per job based
/// on the profile's format + encode mode and hands the encode off to it.
///
/// Each strategy composes injectable building blocks (filter graph builder,
/// playlist generator, subtitle extractor, …) rather than baking format-specific
/// logic into the shared stages.
///
/// Extends <see cref="IStageOverrides"/> so every strategy implicitly carries
/// the hook. All override properties default to null — existing strategies
/// require zero changes.
/// </summary>
public interface IEncodingStrategy : IStageOverrides
{
    /// <summary>
    /// Output container this strategy produces (HLS, MKV, MP4, DASH, …).
    /// </summary>
    OutputFormat Format { get; }

    /// <summary>
    /// Encode mode this strategy handles (SinglePass, TwoPass).
    /// </summary>
    EncodeMode EncodeMode { get; }

    /// <summary>
    /// Execute the full encode. Strategies MAY early-return with a
    /// <see cref="StreamAction.Drop"/>-style result when the request is
    /// incompatible — validation lives upstream in the orchestrator.
    /// </summary>
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    );

    /// <summary>
    /// Decompose the resolved <paramref name="plan"/> into independent tasks
    /// that can each be enqueued as a separate child job. Strategies that
    /// genuinely cannot split (MP4 single-pass, MKV, audio-only) return a
    /// single <see cref="EncodeTaskKind.Whole"/> task.
    ///
    /// <para>The default implementation returns a single Whole task so all
    /// existing strategies remain valid without any changes.</para>
    ///
    /// <para><paramref name="groupTag"/> is a caller-supplied ULID string
    /// that groups all tasks from one coordinator run so child jobs can be
    /// queried as a set.</para>
    /// </summary>
    DecomposedTask[] Decompose(OutputPlan plan, string groupTag) => [WholeTask(groupTag: groupTag)];

    /// <summary>
    /// Helper used by the default <see cref="Decompose"/> implementation and
    /// by strategies that still need a fallback whole task.
    /// </summary>
    static DecomposedTask WholeTask(string groupTag) =>
        new(
            TaskId: $"{groupTag}-whole",
            ParentJobId: 0,
            GroupTag: groupTag,
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            EstimatedCostUnits: 1,
            Label: "whole"
        );
}
