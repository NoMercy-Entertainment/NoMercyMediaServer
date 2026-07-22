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

using Microsoft.Extensions.Logging;
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Remote;

namespace NoMercy.Storage.Factory;

public sealed class SmbDriverBuilder : IStorageDriverBuilder
{
    private readonly ILogger _logger;
    private readonly ICredentialResolver? _credentialResolver;

    public SmbDriverBuilder(ILogger logger, ICredentialResolver? credentialResolver = null)
    {
        _logger = logger;
        _credentialResolver = credentialResolver;
    }

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["smb"];

    public IStorage Build(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string subPath
    )
    {
        if (string.IsNullOrWhiteSpace(value: driverConfigJson))
            throw new ArgumentException(
                message: $"driver_config is required for 'smb' (folder {folderId}). "
                         + "Supply at minimum: host, share.",
                paramName: nameof(driverConfigJson)
            );

        SmbDriverConfig config = SmbDriverConfig.Parse(json: driverConfigJson, folderId: folderId, logger: _logger);

        string? username = null;
        string? password = null;
        if (_credentialResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = _credentialResolver.Resolve(
                credentialsRef: $"driver:{folderId}"
            );
            if (creds is not null)
            {
                username = creds.Value.AccessKey;
                password = creds.Value.SecretKey;
            }
            else
            {
                _logger.LogWarning(
                    message: "No credentials found in store for SMB driver (folder {FolderId}); connecting with empty credentials",
                    args: folderId
                );
            }
        }

        config = config with { Username = username, Password = password };

        // Fold the folder's sub-path into the share-relative BasePath.
        if (!string.IsNullOrEmpty(value: subPath))
        {
            string combined = StorageFactory.JoinRoot(root: config.BasePath, subPath: subPath, driverType: "smb");
            config = config with { BasePath = combined.Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/') };
        }

        SmbStorageDriver driver = new(config: config, log: _logger);
        return new RemoteStorage(driver: driver);
    }
}
