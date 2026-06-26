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

namespace NoMercy.Storage.DriverGrouping;

/// <summary>
/// A folder's assignment within a <see cref="DriverGroup"/>.
/// </summary>
/// <param name="FolderId">The folder's stable ULID.</param>
/// <param name="SubPath">
/// Path relative to the driver root. Empty string means the folder IS
/// the driver root. Forward slashes for SMB, OS separator for local.
/// </param>
public sealed record FolderAssignment(Ulid FolderId, string SubPath);
