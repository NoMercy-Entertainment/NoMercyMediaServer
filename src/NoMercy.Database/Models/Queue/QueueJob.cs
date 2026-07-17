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
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models.Queue;

[PrimaryKey(nameof(Id))]
// Matches the task-list sort (Priority desc, CreatedAt asc) so SQLite serves it
// from the index instead of sorting the whole growing queue table.
[Index(nameof(Priority), nameof(CreatedAt), IsDescending = new[] { true, false })]
// Every Dispatch dedups via QueueJobs.Any(j => j.Payload == x). Without this index
// that is a full scan of the (large, history-retaining) queue table — seconds per
// enqueue, which surfaced as multi-second endpoints that dispatch a job inline.
[Index(nameof(Payload))]
// Every worker poll reserves via (Queue, ReservedAt IS NULL, Attempts, AvailableAt)
// ordered by (Priority desc, CreatedAt, Id). The sort index above cannot serve that
// — the predicate leads with Queue — so SQLite fell back to SCAN + a temp B-tree
// over the whole table: ~2.7s per reserve at 116k rows, taken while JobQueue holds
// its global write lock, which serialises every worker on every queue. Leading with
// Queue and ReservedAt turns it into a SEARCH and lets the trailing columns satisfy
// the ORDER BY. Measured 2741ms -> 87ms on a copy of a real 2.5GB queue.db.
[Index(
    nameof(Queue),
    nameof(ReservedAt),
    nameof(Priority),
    nameof(CreatedAt),
    nameof(Id),
    IsDescending = new[] { false, false, true, false, false }
)]
public class QueueJob
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int Priority { get; set; }
    public string Queue { get; set; } = "default";

    [MaxLength(4096)]
    public required string Payload { get; set; }
    public byte Attempts { get; set; } = 0;
    public DateTime? ReservedAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the coordinator job that spawned this child task.
    /// Null for top-level (non-decomposed) jobs.
    /// </summary>
    public int? ParentJobId { get; set; }

    /// <summary>
    /// Shared ULID tag for all tasks spawned by a single encode coordinator run.
    /// Null for non-decomposed jobs.
    /// </summary>
    [MaxLength(64)]
    public string? GroupTag { get; set; }
}
