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

namespace NoMercy.Storage.Drivers.Smb;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for SMB/CIFS folder
/// drivers (Samba, Windows shares, Synology/QNAP SMB). Credentials (username /
/// password) are not stored in the config JSON — they are resolved from the
/// credential store by <see cref="NoMercy.Storage.Factory.StorageFactory"/> and
/// injected via <see cref="Username"/> / <see cref="Password"/> after parse.
/// </summary>
public sealed record SmbDriverConfig(string Host, string Share, int Port, int TimeoutSeconds)
{
    /// <summary>Auth username — set by the factory after credential resolution.</summary>
    public string? Username { get; init; }

    /// <summary>Auth password — set by the factory after credential resolution.</summary>
    public string? Password { get; init; }

    /// <summary>Optional NTLM domain/workgroup (empty for local accounts).</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Share-relative sub-path (forward-slash form, no leading slash). The
    /// builder appends the folder's sub-path here; operations are resolved
    /// relative to it.
    /// </summary>
    public string BasePath { get; init; } = string.Empty;

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses and validates the JSON config blob from <c>Folder.DriverConfig</c>.
    /// Throws <see cref="ArgumentException"/> on missing or invalid fields.
    /// </summary>
    public static SmbDriverConfig Parse(string json, Ulid folderId, ILogger? logger = null)
    {
        SmbDriverConfigRaw? raw;
        try
        {
            raw = JsonSerializer.Deserialize<SmbDriverConfigRaw>(json: json, options: ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                message: $"Failed to parse driver_config for SMB folder {folderId}: {ex.Message}",
                paramName: nameof(json),
                innerException: ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                message: $"driver_config deserialized to null for SMB folder {folderId}.",
                paramName: nameof(json)
            );

        if (string.IsNullOrWhiteSpace(value: raw.Host))
            throw new ArgumentException(
                message: $"driver_config.host is required for SMB folder {folderId}.",
                paramName: nameof(json)
            );

        if (string.IsNullOrWhiteSpace(value: raw.Share))
            throw new ArgumentException(
                message: $"driver_config.share is required for SMB folder {folderId}.",
                paramName: nameof(json)
            );

        int port = raw.Port ?? 445;
        if (port is <= 0 or > 65535)
            throw new ArgumentException(
                message: $"driver_config.port must be in 1..65535 for SMB folder {folderId} (got {port}).",
                paramName: nameof(json)
            );

        int timeout = raw.TimeoutSeconds ?? 30;
        if (timeout <= 0)
            throw new ArgumentException(
                message: $"driver_config.timeoutSeconds must be positive for SMB folder {folderId} (got {timeout}).",
                paramName: nameof(json)
            );

        return new(Host: raw.Host.Trim(), Share: NormalizeShare(share: raw.Share), Port: port, TimeoutSeconds: timeout)
        {
            Domain = raw.Domain?.Trim() ?? string.Empty,
            BasePath = NormalizePath(path: raw.Path),
        };
    }

    private static string NormalizeShare(string share) => share.Trim().Trim(trimChars: ['/', '\\']);

    private static string NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(value: path) ? string.Empty : path.Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');

    // -----------------------------------------------------------------------
    // Overload for unit tests that supply individual fields without JSON
    // -----------------------------------------------------------------------
    public static SmbDriverConfig For(
        string host,
        string share,
        string? username = null,
        string? password = null,
        string domain = "",
        string basePath = "",
        int port = 445,
        int timeoutSeconds = 30
    ) =>
        new(Host: host, Share: NormalizeShare(share: share), Port: port, TimeoutSeconds: timeoutSeconds)
        {
            Username = username,
            Password = password,
            Domain = domain,
            BasePath = NormalizePath(path: basePath),
        };

    private sealed record SmbDriverConfigRaw(
        [property: JsonPropertyName(name: "host")] string? Host,
        [property: JsonPropertyName(name: "share")] string? Share,
        [property: JsonPropertyName(name: "path")] string? Path,
        [property: JsonPropertyName(name: "domain")] string? Domain,
        [property: JsonPropertyName(name: "port")] int? Port,
        [property: JsonPropertyName(name: "timeoutSeconds")] int? TimeoutSeconds
    );
}
