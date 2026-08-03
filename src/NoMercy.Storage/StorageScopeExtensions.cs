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

namespace NoMercy.Storage;

public static class StorageScopeExtensions
{
    /// <summary>
    /// Resolves a scope-relative folder key ("Download/complete/Blondie - Greatest Hits
    /// (2002)") to the backend-absolute path the raw driver's file APIs
    /// (EnumerateFileSystemEntries, DirectoryExists, MediaScan) expect.
    /// <para>
    /// A local library's root lives in the LocalStorage facade's path guard, not in the
    /// stateless local driver — its GetFullPath is a bare Path.GetFullPath that
    /// canonicalizes against the process working directory. Resolving a scope-relative
    /// key through the driver therefore silently produces a path under the working
    /// directory, the scan matches nothing, and the caller reports an empty folder
    /// instead of an error. Remote backends (NFS / S3 / SMB / WebDAV) carry their own
    /// export/bucket root inside the driver and reject the facade call, so they resolve
    /// through the driver.
    /// </para>
    /// <para>
    /// Anything that hands a user-supplied folder to a raw <see cref="IStorageDriver"/>
    /// has to come through here first. It has cost the local-library rescan path and the
    /// music file list the same silent zero-result bug on separate occasions.
    /// </para>
    /// </summary>
    public static string ResolveBackendPath(this IStorage storage, string scopeRelativePath)
    {
        try
        {
            return storage.GetFullPath(scopeRelativePath);
        }
        catch (NotSupportedException)
        {
            return storage.Driver.GetFullPath(scopeRelativePath);
        }
    }
}
