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

using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Dto;

namespace NoMercy.MediaProcessing.Libraries;

public interface ILibraryRepository : IDisposable, IAsyncDisposable
{
    public Task<IEnumerable<MediaFolderExtend>> GetRootFoldersAsync(string path);

    public Task<Library?> GetLibraryWithFolders(Ulid id);

    public Task<Folder?> GetLibraryFolder(Ulid folderId);

    public Task<Library?> GetLibraryByIdWithFolders(Ulid libraryId);

    public Task<HashSet<string>> GetExistingFolderNamesAsync(Ulid libraryId, string libraryType);
}
