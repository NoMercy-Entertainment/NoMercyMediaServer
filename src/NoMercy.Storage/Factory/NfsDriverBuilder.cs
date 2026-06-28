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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;
using static NoMercy.Storage.Factory.StorageFactory;

namespace NoMercy.Storage.Factory;

public sealed class NfsDriverBuilder : IStorageDriverBuilder
{
    private readonly ILogger _logger;

    public NfsDriverBuilder(ILogger logger) => _logger = logger;

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["nfs"];

    public IStorage Build(Ulid folderId, string driverType, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for 'nfs' (folder {folderId}). "
                    + "Supply at minimum: server and export.",
                nameof(driverConfigJson)
            );

        NfsDriverConfig nfsConfig = NfsDriverConfig.Parse(driverConfigJson, folderId);

        // Record the folder sub-path as SubPath so the mount stays at the
        // export root. Baking subPath into Export caused libnfs to try mounting
        // a non-existent export (e.g. /mnt/vault/Media/Anime instead of /mnt/vault/Media).
        if (!string.IsNullOrEmpty(subPath))
        {
            nfsConfig = nfsConfig with { SubPath = subPath.Replace('\\', '/').Trim('/') };
        }

        NfsStorageDriver nfsDriver = new(nfsConfig, _logger);
        return new RemoteStorage(nfsDriver);
    }

}
