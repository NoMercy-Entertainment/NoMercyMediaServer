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

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models.Encoder;

/// <summary>
/// Durable record of a single completed <c>EncodeTaskJob</c> execution.
/// Written by the child job before publishing the EventBus event so the
/// coordinator can poll this table on restart rather than relying on the
/// in-memory subscription that evaporates with the process.
/// </summary>
[PrimaryKey(nameof(TaskId))]
[Index(nameof(ParentJobId))]
[Index(nameof(GroupTag))]
public class EncodeTaskOutcome
{
    /// <summary>Unique task identifier matching <c>DecomposedTask.TaskId</c>.</summary>
    [MaxLength(256)]
    public required string TaskId { get; set; }

    /// <summary>Queue-job ID of the coordinator that owns this task.</summary>
    public required int ParentJobId { get; set; }

    /// <summary>Shared run tag — all tasks from one coordinator run share this.</summary>
    [MaxLength(256)]
    public required string GroupTag { get; set; }

    /// <summary>True when the task completed without errors.</summary>
    public required bool Success { get; set; }

    /// <summary>Human-readable error description, null on success.</summary>
    [MaxLength(4096)]
    public string? ErrorMessage { get; set; }

    /// <summary>Kind of work serialized as text (EncodeTaskKind enum name).</summary>
    [MaxLength(64)]
    public required string Kind { get; set; }

    /// <summary>Newline-joined artifact paths produced by this task, or null when none.</summary>
    [MaxLength(int.MaxValue)]
    public string? OutputArtifactsJson { get; set; }

    /// <summary>UTC timestamp of completion.</summary>
    public required DateTime CompletedAt { get; set; }
}
