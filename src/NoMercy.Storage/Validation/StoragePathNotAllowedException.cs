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

namespace NoMercy.Storage.Validation;

/// <summary>
/// Raised when <see cref="StoragePathGuard"/> rejects a path. Surfaces
/// to controllers as the <c>output.path_not_allowed</c> runtime-error
/// rule (catalogued in encoder Phase 4.19).
/// </summary>
public sealed class StoragePathNotAllowedException(string attemptedPath, string reason)
    : InvalidOperationException($"output.path_not_allowed: {reason} (path={attemptedPath})")
{
    public string AttemptedPath { get; } = attemptedPath;
    public string Reason { get; } = reason;
}
