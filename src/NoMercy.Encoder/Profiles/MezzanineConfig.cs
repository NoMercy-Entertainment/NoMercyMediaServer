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

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Configures the near-lossless full-res SDR intermediate (mezzanine) used by the
/// distributed-encode derivation path: the expensive decode + tonemap runs once
/// into this mezzanine, then each ladder rung is derived from it on any worker
/// with no re-decode / re-tonemap and no generational quality loss.
///
/// Default is visually-lossless HEVC (small on disk, fast to re-decode). Set a
/// lossless codec (e.g. ffv1) for zero compromise at the cost of size + IO.
/// Only materialised when the planner derives across a process/worker boundary;
/// fused single-process encodes never write a mezzanine.
/// </summary>
public record MezzanineConfig(string Codec = "hevc", int Crf = 12, string Preset = "medium");
