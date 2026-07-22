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
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Remote;
using static NoMercy.Storage.Factory.StorageFactory;

namespace NoMercy.Storage.Factory;

public sealed class WebDavDriverBuilder : IStorageDriverBuilder
{
    private readonly ILogger _logger;
    private readonly ICredentialResolver? _credentialResolver;

    public WebDavDriverBuilder(ILogger logger, ICredentialResolver? credentialResolver = null)
    {
        _logger = logger;
        _credentialResolver = credentialResolver;
    }

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["webdav"];

    public IStorage Build(Ulid folderId, string driverType, string? driverConfigJson, string subPath)
    {
        if (string.IsNullOrWhiteSpace(value: driverConfigJson))
            throw new ArgumentException(
                message: $"driver_config is required for 'webdav' (folder {folderId}). "
                         + "Supply at minimum: url.",
                paramName: nameof(driverConfigJson)
            );

        WebDavDriverConfig webDavConfig = WebDavDriverConfig.Parse(
            json: driverConfigJson,
            folderId: folderId,
            logger: _logger
        );

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
                    message: "No credentials found in store for WebDAV driver (folder {FolderId}); connecting anonymously",
                    args: folderId
                );
            }
        }

        webDavConfig = webDavConfig with { Username = username, Password = password };

        // Append sub-path to the WebDAV base URL when non-empty.
        if (!string.IsNullOrEmpty(value: subPath))
        {
            string combinedUrl = JoinRoot(root: webDavConfig.Url, subPath: subPath, driverType: "webdav");
            webDavConfig = webDavConfig with { Url = combinedUrl };
        }

        WebDavStorageDriver webDavDriver = new(config: webDavConfig);
        return new RemoteStorage(driver: webDavDriver);
    }

}
