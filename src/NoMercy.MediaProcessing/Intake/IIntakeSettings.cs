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

namespace NoMercy.MediaProcessing.Intake;

public interface IIntakeSettings
{
    Task<string?> GetDropFolderAsync(CancellationToken ct);

    Task SetDropFolderAsync(string? path, CancellationToken ct);

    Task<bool> HasTokenAsync(CancellationToken ct);

    Task<string> IssueTokenAsync(CancellationToken ct);

    Task<bool> VerifyTokenAsync(string? presented, CancellationToken ct);
}
