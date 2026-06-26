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

namespace NoMercy.Encoder.SystemFeatures;

public interface INvencSessionManager
{
    int DetectedSessionLimit { get; }

    bool IsPatchAvailable { get; }

    bool IsPatched { get; }

    bool CanPatch { get; }

    Task<PatchResult> ApplyPatchAsync(CancellationToken ct);
}

public record PatchResult(bool Success, string Message, bool RequiresRestart);
