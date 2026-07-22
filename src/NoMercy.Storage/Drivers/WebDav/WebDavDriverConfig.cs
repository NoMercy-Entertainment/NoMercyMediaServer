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

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NoMercy.Storage.Drivers.WebDav;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for WebDAV folder drivers.
/// Supports Nextcloud, ownCloud, Synology DSM, SharePoint, and generic mod_dav servers.
/// Credentials (username / password) are not stored in the config JSON — they are
/// resolved from the credential store by <see cref="NoMercy.Storage.Factory.StorageFactory"/>
/// and injected via <see cref="Username"/> / <see cref="Password"/> after construction.
/// </summary>
internal sealed record WebDavDriverConfig(string Url, bool IgnoreCertErrors, int TimeoutSeconds)
{
    /// <summary>Basic-auth username — set by factory after credential resolution.</summary>
    internal string? Username { get; init; }

    /// <summary>Basic-auth password — set by factory after credential resolution.</summary>
    internal string? Password { get; init; }

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses and validates the JSON config blob from <c>Folder.DriverConfig</c>.
    /// Throws <see cref="ArgumentException"/> on missing or invalid fields.
    /// Logs a warning when legacy fields (<c>username</c>, <c>passwordRef</c>,
    /// <c>bearerTokenRef</c>) are present in the JSON; they are ignored.
    /// </summary>
    internal static WebDavDriverConfig Parse(string json, Ulid folderId, ILogger? logger = null)
    {
        WebDavDriverConfigRaw? raw;
        try
        {
            raw = JsonSerializer.Deserialize<WebDavDriverConfigRaw>(json: json, options: ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                message: $"Failed to parse driver_config for WebDAV folder {folderId}: {ex.Message}",
                paramName: nameof(json),
                innerException: ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                message: $"driver_config deserialized to null for WebDAV folder {folderId}.",
                paramName: nameof(json)
            );

        if (string.IsNullOrWhiteSpace(value: raw.Url))
            throw new ArgumentException(
                message: $"driver_config.url is required for WebDAV folder {folderId}.",
                paramName: nameof(json)
            );

        if (raw.HasLegacyFields)
        {
            logger?.LogWarning(
                message: "WebDAV folder {FolderId} driver_config contains deprecated fields "
                         + "(username / passwordRef / bearerTokenRef). These fields are ignored. "
                         + "Re-save the driver to migrate credentials to the unified credentials store.",
                args: folderId
            );
        }

        int timeout = raw.TimeoutSeconds ?? 30;
        if (timeout <= 0)
            throw new ArgumentException(
                message: $"driver_config.timeoutSeconds must be positive for WebDAV folder {folderId} (got {timeout}).",
                paramName: nameof(json)
            );

        return new(
            Url: NormalizeUrl(url: raw.Url.Trim()),
            IgnoreCertErrors: raw.IgnoreCertErrors ?? false,
            TimeoutSeconds: timeout
        );
    }

    private static string NormalizeUrl(string url)
    {
        return url.TrimEnd(trimChar: '/') + "/";
    }

    // -----------------------------------------------------------------------
    // Overload for unit tests that supply individual fields without JSON
    // -----------------------------------------------------------------------
    internal static WebDavDriverConfig For(
        string url,
        string? username = null,
        string? password = null,
        bool ignoreCertErrors = false,
        int timeoutSeconds = 30
    ) =>
        new(Url: NormalizeUrl(url: url), IgnoreCertErrors: ignoreCertErrors, TimeoutSeconds: timeoutSeconds)
        {
            Username = username,
            Password = password,
        };

    // -----------------------------------------------------------------------
    // Raw deserialization target (case-insensitive keys)
    // -----------------------------------------------------------------------
    private sealed record WebDavDriverConfigRaw(
        [property: JsonPropertyName(name: "url")] string? Url,
        [property: JsonPropertyName(name: "ignoreCertErrors")] bool? IgnoreCertErrors,
        [property: JsonPropertyName(name: "timeoutSeconds")] int? TimeoutSeconds,
        // Legacy fields — detected for warning only, values discarded.
        [property: JsonPropertyName(name: "username")] string? LegacyUsername = null,
        [property: JsonPropertyName(name: "passwordRef")] string? LegacyPasswordRef = null,
        [property: JsonPropertyName(name: "bearerTokenRef")] string? LegacyBearerTokenRef = null
    )
    {
        internal bool HasLegacyFields =>
            !string.IsNullOrWhiteSpace(value: LegacyUsername)
            || !string.IsNullOrWhiteSpace(value: LegacyPasswordRef)
            || !string.IsNullOrWhiteSpace(value: LegacyBearerTokenRef);
    }
}
