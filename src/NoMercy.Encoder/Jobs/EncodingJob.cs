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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Jobs;

public record EncodingJob(
    string JobId,
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    JobCheckpoint? Checkpoint,
    DateTime CreatedAtUtc,
    string? HmacSignature = null
);
