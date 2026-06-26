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
/// One shared-root driver group produced by <see cref="StorageDriverGrouper"/>.
/// </summary>
/// <param name="DriverRoot">
/// The absolute path that becomes <c>Driver.Config.rootPath</c>.
/// All folders in this group live under (or at) this root.
/// </param>
/// <param name="DriverType">
/// <c>"smb"</c> for UNC paths; <c>"local"</c> for everything else.
/// </param>
/// <param name="Folders">
/// One entry per folder in the group.
/// <c>SubPath</c> is the path relative to <see cref="DriverRoot"/>
/// using the folder separator appropriate to the driver type
/// (forward slash for SMB, OS separator for local).
/// </param>
public sealed record DriverGroup(
    string DriverRoot,
    string DriverType,
    IReadOnlyList<FolderAssignment> Folders
);
