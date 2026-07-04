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
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.Files;

/// <summary>
/// Directory media listing for the dashboard file browser. Identification is
/// enrichment: every video file is returned even when TMDB cannot resolve it.
/// </summary>
public interface IFileListService
{
    Task<List<FileItem>> GetFilesInDirectory(string directoryPath, string libraryType);

    Task<List<FileItem>> GetFilesInDirectory(
        string directoryPath,
        string libraryType,
        IStorage storage
    );
}
