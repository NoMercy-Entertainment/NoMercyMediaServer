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
            raw = JsonSerializer.Deserialize<SmbDriverConfigRaw>(json, ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for SMB folder {folderId}: {ex.Message}",
                nameof(json),
                ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                $"driver_config deserialized to null for SMB folder {folderId}.",
                nameof(json)
            );

        if (string.IsNullOrWhiteSpace(raw.Host))
            throw new ArgumentException(
                $"driver_config.host is required for SMB folder {folderId}.",
                nameof(json)
            );

        if (string.IsNullOrWhiteSpace(raw.Share))
            throw new ArgumentException(
                $"driver_config.share is required for SMB folder {folderId}.",
                nameof(json)
            );

        int port = raw.Port ?? 445;
        if (port is <= 0 or > 65535)
            throw new ArgumentException(
                $"driver_config.port must be in 1..65535 for SMB folder {folderId} (got {port}).",
                nameof(json)
            );

        int timeout = raw.TimeoutSeconds ?? 30;
        if (timeout <= 0)
            throw new ArgumentException(
                $"driver_config.timeoutSeconds must be positive for SMB folder {folderId} (got {timeout}).",
                nameof(json)
            );

        return new(raw.Host.Trim(), NormalizeShare(raw.Share), port, timeout)
        {
            Domain = raw.Domain?.Trim() ?? string.Empty,
            BasePath = NormalizePath(raw.Path),
        };
    }

    private static string NormalizeShare(string share) => share.Trim().Trim('/', '\\');

    private static string NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').Trim('/');

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
        new(host, NormalizeShare(share), port, timeoutSeconds)
        {
            Username = username,
            Password = password,
            Domain = domain,
            BasePath = NormalizePath(basePath),
        };

    private sealed record SmbDriverConfigRaw(
        [property: JsonPropertyName("host")] string? Host,
        [property: JsonPropertyName("share")] string? Share,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("domain")] string? Domain,
        [property: JsonPropertyName("port")] int? Port,
        [property: JsonPropertyName("timeoutSeconds")] int? TimeoutSeconds
    );
}
