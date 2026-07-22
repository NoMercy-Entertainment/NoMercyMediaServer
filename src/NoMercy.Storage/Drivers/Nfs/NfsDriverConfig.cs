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

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for NFS folder drivers.
/// Drives in-process NFS via libnfs P/Invoke — no OS-level mount required.
/// </summary>
public sealed record NfsDriverConfig(
    string Server,
    string Export,
    int Version,
    int? Uid,
    int? Gid,
    int Port,
    int? MountPort,
    string SubPath = ""
)
{
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses and validates the JSON config blob from <c>Folder.DriverConfig</c>.
    /// Throws <see cref="ArgumentException"/> on missing required fields.
    /// </summary>
    internal static NfsDriverConfig Parse(string json, Ulid folderId)
    {
        NfsDriverConfigRaw? raw;
        try
        {
            raw = JsonSerializer.Deserialize<NfsDriverConfigRaw>(json: json, options: ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                message: $"Failed to parse driver_config for NFS folder {folderId}: {ex.Message}",
                paramName: nameof(json),
                innerException: ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                message: $"driver_config deserialized to null for NFS folder {folderId}.",
                paramName: nameof(json)
            );

        if (string.IsNullOrWhiteSpace(value: raw.Server))
            throw new ArgumentException(
                message: $"driver_config.server is required for NFS folder {folderId}.",
                paramName: nameof(json)
            );

        if (string.IsNullOrWhiteSpace(value: raw.Export))
            throw new ArgumentException(
                message: $"driver_config.export is required for NFS folder {folderId}.",
                paramName: nameof(json)
            );

        int version = raw.Version ?? 3;
        if (version != 3 && version != 4)
            throw new ArgumentException(
                message: $"driver_config.version must be 3 or 4 for NFS folder {folderId} (got {version}).",
                paramName: nameof(json)
            );

        return new(
            Server: raw.Server.Trim(),
            Export: NormalizeExport(export: raw.Export.Trim()),
            Version: version,
            Uid: raw.Uid,
            Gid: raw.Gid,
            Port: raw.Port ?? 2049,
            MountPort: raw.MountPort
        );
    }

    private static string NormalizeExport(string export)
    {
        string normalized = export.Replace(oldChar: '\\', newChar: '/');
        if (!normalized.StartsWith(value: '/'))
            normalized = "/" + normalized;
        return normalized.TrimEnd(trimChar: '/') is "" ? "/" : normalized.TrimEnd(trimChar: '/');
    }

    // -----------------------------------------------------------------------
    // Overload for unit tests that supply individual fields without JSON
    // -----------------------------------------------------------------------
    public static NfsDriverConfig For(
        string server,
        string export,
        int version = 3,
        int? uid = null,
        int? gid = null,
        int port = 2049,
        int? mountPort = null
    ) => new(Server: server, Export: NormalizeExport(export: export), Version: version, Uid: uid, Gid: gid, Port: port, MountPort: mountPort);

    // -----------------------------------------------------------------------
    // Raw deserialization target (snake_case keys)
    // -----------------------------------------------------------------------
    private sealed record NfsDriverConfigRaw(
        string? Server,
        string? Export,
        int? Version,
        int? Uid,
        int? Gid,
        int? Port,
        int? MountPort
    );
}
