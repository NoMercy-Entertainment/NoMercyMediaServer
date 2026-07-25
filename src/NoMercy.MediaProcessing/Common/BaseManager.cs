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

using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.MediaProcessing.Common;

public class BaseManager : IBaseManager
{
    public string BaseUrl(string title, DateTime? releaseDate)
    {
        return "/" + string.Concat([title, ".(", releaseDate.ParseYear(), ")"]).CleanFileName();
    }

    public string BaseUrl(string name)
    {
        return string.Concat(name[0], "/", name).CleanFileName();
    }

    /// <summary>
    /// Returns the root path in the form the storage's driver understands.
    /// LocalStorage resolves the scope-relative <paramref name="path"/> to an
    /// OS-absolute path via <see cref="IStorage.GetFullPath"/>. Remote storages
    /// (NFS, S3, WebDAV, R2) work entirely in scope-relative paths, so
    /// <paramref name="path"/> is returned as-is. Never call
    /// <see cref="IStorage.GetFullPath"/> unconditionally — it throws for any
    /// non-local backend.
    /// </summary>
    protected static string FolderRootPath(IStorage storage, string path) =>
        storage is LocalStorage ? storage.GetFullPath(path) : path;
}
