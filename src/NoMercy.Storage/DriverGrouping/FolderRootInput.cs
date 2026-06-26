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
/// Input descriptor for the pure grouping function.
/// </summary>
/// <param name="FolderId">Stable identifier for the folder.</param>
/// <param name="AbsoluteRootPath">
/// The folder's current absolute path on disk — either the old V1
/// <c>Folder.Path</c> (a full absolute path) or the per-folder driver's
/// <c>Config.rootPath</c> when the earlier migration has already run.
/// </param>
public sealed record FolderRootInput(Ulid FolderId, string AbsoluteRootPath);
