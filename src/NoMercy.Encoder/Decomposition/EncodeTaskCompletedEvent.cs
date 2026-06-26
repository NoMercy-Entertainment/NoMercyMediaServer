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

using NoMercy.Events;

namespace NoMercy.Encoder.Decomposition;

/// <summary>
/// Published by <c>EncodeTaskJob</c> when a child encode task finishes
/// (successfully or otherwise). The parent coordinator subscribes on the
/// event bus and re-evaluates whether all children are accounted for.
/// </summary>
public sealed class EncodeTaskCompletedEvent : EventBase
{
    public override string Source => "Encoder";

    /// <summary>Unique task ID matching <see cref="DecomposedTask.TaskId"/>.</summary>
    public required string TaskId { get; init; }

    /// <summary>Queue-job ID of the coordinator that owns this task.</summary>
    public required int ParentJobId { get; init; }

    /// <summary>Encode run tag matching <see cref="DecomposedTask.GroupTag"/>.</summary>
    public required string GroupTag { get; init; }

    /// <summary>True when the task completed without errors.</summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Paths of files produced by this task. Empty when the task failed.
    /// Used by the coordinator to assemble the final artifact list.
    /// </summary>
    public IReadOnlyList<string> OutputArtifacts { get; init; } = [];

    /// <summary>Human-readable error description, or null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Kind of work this task represented.</summary>
    public EncodeTaskKind Kind { get; init; }
}
