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

namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// Layer 1 output of <see cref="DecodeAwareBundlePlanner"/>: every Video-kind
/// task index (into the <c>DecomposedTask[]</c> the planner was given) that
/// shares <see cref="Class"/>, and — for <see cref="DecodeClass.Tonemap"/> —
/// the exact tonemap chain they share. Pure classification, no capacity
/// concerns; <see cref="DecodeAwareBundlePlanner.Plan"/> is what turns a
/// group into one-or-more capacity-bounded ffmpeg bundles (Layer 2).
/// </summary>
public sealed record DecodeGroup(
    DecodeClass Class,
    /// <summary>
    /// The tonemap filter chain every task in this group shares. Only set
    /// (and only ever compared) for <see cref="DecodeClass.Tonemap"/> groups
    /// — two Tonemap rungs with different chains are NOT the same decode
    /// group and land in separate <see cref="DecodeGroup"/> instances.
    /// </summary>
    string? TonemapChain,
    /// <summary>Indexes into the source <c>DecomposedTask[]</c> of every Video task in this group.</summary>
    IReadOnlyList<int> VideoTaskIndexes
);
