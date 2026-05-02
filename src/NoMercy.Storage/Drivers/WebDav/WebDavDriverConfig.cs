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
            raw = JsonSerializer.Deserialize<WebDavDriverConfigRaw>(json, ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"Failed to parse driver_config for WebDAV folder {folderId}: {ex.Message}",
                nameof(json),
                ex
            );
        }

        if (raw is null)
            throw new ArgumentException(
                $"driver_config deserialized to null for WebDAV folder {folderId}.",
                nameof(json)
            );

        if (string.IsNullOrWhiteSpace(raw.Url))
            throw new ArgumentException(
                $"driver_config.url is required for WebDAV folder {folderId}.",
                nameof(json)
            );

        if (raw.HasLegacyFields)
        {
            logger?.LogWarning(
                "WebDAV folder {FolderId} driver_config contains deprecated fields "
                    + "(username / passwordRef / bearerTokenRef). These fields are ignored. "
                    + "Re-save the driver to migrate credentials to the unified credentials store.",
                folderId
            );
        }

        int timeout = raw.TimeoutSeconds ?? 30;
        if (timeout <= 0)
            throw new ArgumentException(
                $"driver_config.timeoutSeconds must be positive for WebDAV folder {folderId} (got {timeout}).",
                nameof(json)
            );

        return new WebDavDriverConfig(
            NormalizeUrl(raw.Url.Trim()),
            raw.IgnoreCertErrors ?? false,
            timeout
        );
    }

    private static string NormalizeUrl(string url)
    {
        return url.TrimEnd('/') + "/";
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
        new(NormalizeUrl(url), ignoreCertErrors, timeoutSeconds)
        {
            Username = username,
            Password = password,
        };

    // -----------------------------------------------------------------------
    // Raw deserialization target (case-insensitive keys)
    // -----------------------------------------------------------------------
    private sealed record WebDavDriverConfigRaw(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("ignoreCertErrors")] bool? IgnoreCertErrors,
        [property: JsonPropertyName("timeoutSeconds")] int? TimeoutSeconds,
        // Legacy fields — detected for warning only, values discarded.
        [property: JsonPropertyName("username")] string? LegacyUsername = null,
        [property: JsonPropertyName("passwordRef")] string? LegacyPasswordRef = null,
        [property: JsonPropertyName("bearerTokenRef")] string? LegacyBearerTokenRef = null
    )
    {
        internal bool HasLegacyFields =>
            !string.IsNullOrWhiteSpace(LegacyUsername)
            || !string.IsNullOrWhiteSpace(LegacyPasswordRef)
            || !string.IsNullOrWhiteSpace(LegacyBearerTokenRef);
    }
}
