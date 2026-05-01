using System.Text.Json;

namespace NoMercy.Storage.Drivers.Nfs;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for NFS folder drivers.
/// Drives in-process NFS via libnfs P/Invoke — no OS-level mount required.
/// </summary>
internal sealed record NfsDriverConfig(
    string Server,
    string Export,
    int Version,
    int? Uid,
    int? Gid,
    int Port,
    int? MountPort
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
            raw = JsonSerializer.Deserialize<NfsDriverConfigRaw>(json, ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for NFS folder {folderId}: {ex.Message}",
                nameof(json),
                ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                $"driver_config deserialized to null for NFS folder {folderId}.",
                nameof(json)
            );

        if (string.IsNullOrWhiteSpace(raw.Server))
            throw new ArgumentException(
                $"driver_config.server is required for NFS folder {folderId}.",
                nameof(json)
            );

        if (string.IsNullOrWhiteSpace(raw.Export))
            throw new ArgumentException(
                $"driver_config.export is required for NFS folder {folderId}.",
                nameof(json)
            );

        int version = raw.Version ?? 3;
        if (version != 3 && version != 4)
            throw new ArgumentException(
                $"driver_config.version must be 3 or 4 for NFS folder {folderId} (got {version}).",
                nameof(json)
            );

        return new NfsDriverConfig(
            raw.Server.Trim(),
            NormalizeExport(raw.Export.Trim()),
            version,
            raw.Uid,
            raw.Gid,
            raw.Port ?? 2049,
            raw.MountPort
        );
    }

    private static string NormalizeExport(string export)
    {
        string normalized = export.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;
        return normalized.TrimEnd('/') is "" ? "/" : normalized.TrimEnd('/');
    }

    // -----------------------------------------------------------------------
    // Overload for unit tests that supply individual fields without JSON
    // -----------------------------------------------------------------------
    internal static NfsDriverConfig For(
        string server,
        string export,
        int version = 3,
        int? uid = null,
        int? gid = null,
        int port = 2049,
        int? mountPort = null
    ) => new(server, export, version, uid, gid, port, mountPort);

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
