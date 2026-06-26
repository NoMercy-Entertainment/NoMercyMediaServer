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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Complexity hint supplied by the caller to tune which rungs the auto-ladder
/// includes. The real complexity probe (scene analysis) lands in a later phase;
/// until then, callers set this manually or leave it as <see cref="Auto"/>.
/// </summary>
public enum ComplexityHint
{
    /// <summary>
    /// Treat as <see cref="LiveAction"/>. Emits a <c>plan.ladder_complexity_unknown</c>
    /// decision so the caller knows it defaulted.
    /// </summary>
    Auto,

    /// <summary>
    /// Flat colour fields compress aggressively — thin the rung set so we don't
    /// waste encode time on redundant bitrate points.
    /// </summary>
    Animated,

    /// <summary>
    /// Standard rung set — every entry whose resolution fits the source.
    /// </summary>
    LiveAction,

    /// <summary>
    /// Grain forces denser bitrate exploration — keep every rung in the table.
    /// </summary>
    Grainy,
}

/// <summary>
/// Generates a full ABR variant set from a single reference <see cref="VideoOutput"/>
/// and the source stream, honouring user-supplied rungs when provided and applying
/// complexity-aware thinning otherwise.
/// </summary>
public interface ILadderGenerator
{
    /// <summary>
    /// Expand one reference VideoOutput + source into the full variant set the planner
    /// should encode. Honors user-supplied <paramref name="userRungs"/> when non-null and
    /// non-empty; else builds from the default table filtered by source resolution and
    /// complexity.
    /// </summary>
    IReadOnlyList<VideoOutput> Generate(
        VideoOutput reference,
        VideoStreamInfo source,
        LadderRung[]? userRungs,
        IDecisionLogSink decisions,
        ComplexityHint complexity = ComplexityHint.Auto
    );
}
