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

namespace NoMercy.Database.Models.Queue;

/// <summary>
/// Bulk job input that several queued jobs share, stored once and referenced by
/// key instead of copied into each payload.
///
/// <para>A music release is the case this exists for. Every track of an album
/// dispatches its own encode, and each one carried the whole MusicBrainz release
/// graph — around a megabyte — so a ten-track album wrote the same megabyte ten
/// times. Across a library that was 11.7GB of queue holding 0.5GB of distinct
/// data, and the dashboard's own queue poll eventually could not sort it without
/// running the disk out of temp space.</para>
///
/// <para>Rows outlive the job that wrote them only until the sweep runs: a blob
/// with no queued job still referencing it is input for work that is over.</para>
/// </summary>
[PrimaryKey(nameof(Key))]
public class QueueJobBlob
{
    [MaxLength(128)]
    public required string Key { get; set; }

    public required string Data { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
