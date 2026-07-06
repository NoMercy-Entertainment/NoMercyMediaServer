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

namespace NoMercy.MediaProcessing.Libraries;

/// <summary>
/// Attaches a default V2 <c>EncodingPresetFolder</c> link to a newly-created
/// library folder so it starts auto-encoding without manual profile picking.
/// </summary>
public interface IDefaultEncodingPresetLinker
{
    /// <summary>
    /// Attaches the default preset link when <paramref name="folderId"/> has
    /// no preset link yet. No-op (returns <c>false</c>) when the folder
    /// already carries any link — default or user-picked — so callers can
    /// invoke this unconditionally without double-linking.
    /// </summary>
    /// <returns><c>true</c> when a new link was created.</returns>
    Task<bool> AttachDefaultIfMissingAsync(Ulid folderId, CancellationToken ct = default);
}
