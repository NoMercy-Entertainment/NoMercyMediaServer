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

public sealed class S3DriverBuilder : IStorageDriverBuilder
{
    private readonly ILogger _logger;
    private readonly ICredentialResolver? _credentialResolver;

    public S3DriverBuilder(ILogger logger, ICredentialResolver? credentialResolver = null)
    {
        _logger = logger;
        _credentialResolver = credentialResolver;
    }

    public IReadOnlyCollection<string> SupportedTypes { get; } = ["s3", "r2"];

    public IStorage Build(
        Ulid folderId,
        string driverType,
        string? driverConfigJson,
        string subPath
    )
    {
        if (string.IsNullOrWhiteSpace(driverConfigJson))
            throw new ArgumentException(
                $"driver_config is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        S3DriverConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<S3DriverConfig>(
                driverConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for folder {folderId} (type={driverType}): {ex.Message}",
                nameof(driverConfigJson),
                ex
            );
        }

        if (config is null)
            throw new ArgumentException(
                $"driver_config deserialized to null for folder {folderId} (type={driverType}).",
                nameof(driverConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Bucket))
            throw new ArgumentException(
                $"driver_config.bucket is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        if (string.IsNullOrWhiteSpace(config.Region))
            throw new ArgumentException(
                $"driver_config.region is required for '{driverType}' (folder {folderId}).",
                nameof(driverConfigJson)
            );

        if (driverType == "r2" && string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException(
                $"driver_config.endpoint is required for 'r2' (folder {folderId}). "
                    + "Set it to your account's R2 endpoint URL.",
                nameof(driverConfigJson)
            );

        // Combine driver prefix with folder sub-path.
        string effectivePrefix = string.IsNullOrEmpty(subPath)
            ? (config.Prefix ?? string.Empty)
            : JoinRoot(config.Prefix ?? string.Empty, subPath, driverType);

        string? accessKey = null;
        string? secretKey = null;

        // Credential resolution order mirrors WebDavStorageDriver: try the
        // explicit credentials_ref first, then fall back to the per-driver key
        // ("driver:{folderId}") that the dashboard uses by default for stored
        // S3/R2 creds. Without this fallback the AWS SDK silently uses the
        // default credential chain (env-vars / IAM) and surfaces "Unable to get
        // IAM security credentials from EC2 Instance Metadata Service".
        if (_credentialResolver is not null)
        {
            (string AccessKey, string SecretKey)? creds = null;

            if (!string.IsNullOrWhiteSpace(config.CredentialsRef))
            {
                creds = _credentialResolver.Resolve(config.CredentialsRef);
                if (creds is null)
                {
                    _logger.LogWarning(
                        "credentials_ref '{CredentialsRef}' not found in secrets store for folder {FolderId}; trying driver:{FallbackKey} fallback",
                        config.CredentialsRef,
                        folderId,
                        $"driver:{folderId}"
                    );
                }
            }

            creds ??= _credentialResolver.Resolve($"driver:{folderId}");

            if (creds is not null)
            {
                accessKey = creds.Value.AccessKey;
                secretKey = creds.Value.SecretKey;
                _logger.LogInformation(
                    "S3/R2 cred resolved for folder {FolderId}: accessKey starts with '{AkPrefix}' len={AkLen}, secret len={SkLen}",
                    folderId,
                    accessKey.Length >= 4 ? accessKey.Substring(0, 4) : accessKey,
                    accessKey.Length,
                    secretKey.Length
                );
            }
            else
            {
                _logger.LogWarning(
                    "No credentials found in store for {DriverType} driver (folder {FolderId}); falling back to default credential chain",
                    driverType,
                    folderId
                );
            }
        }

        // S3/R2 require explicit credentials in a self-hosted media server
        // context — the default AWS credential chain (env vars, EC2 IMDS) is
        // never what an operator wants here. Reject with a message that
        // names the fix instead of letting the SDK throw "Credential access
        // key has length 0" / "Unable to get IAM credentials from EC2 IMDS"
        // deep inside the signing path.
        bool emptyOrNullAccess = string.IsNullOrEmpty(accessKey);
        bool emptyOrNullSecret = string.IsNullOrEmpty(secretKey);
        if (emptyOrNullAccess || emptyOrNullSecret)
            throw new ArgumentException(
                $"{driverType} driver (folder {folderId}) has no credentials configured. "
                    + "Open the driver in the dashboard and set access key + secret key.",
                nameof(driverConfigJson)
            );

        S3StorageDriver s3Driver = new(
            config.Bucket,
            config.Region,
            effectivePrefix,
            config.Endpoint,
            accessKey,
            secretKey
        );

        return new RemoteStorage(s3Driver);
    }
}
