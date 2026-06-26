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

namespace NoMercy.Encoder.Jobs;

/// <summary>
/// Persists encode job checkpoints so a crashed or cancelled job can resume
/// without redoing completed work (e.g. skip pass 1 in a 2-pass encode).
/// Storage is keyed by output directory — the checkpoint lives next to the
/// encode it describes, so multiple concurrent jobs cannot collide.
/// </summary>
public interface ICheckpointStore
{
    Task SaveAsync(JobCheckpoint checkpoint, CancellationToken ct = default);
    Task<JobCheckpoint?> LoadAsync(string outputDirectory, CancellationToken ct = default);
    Task DeleteAsync(string outputDirectory, CancellationToken ct = default);
}
