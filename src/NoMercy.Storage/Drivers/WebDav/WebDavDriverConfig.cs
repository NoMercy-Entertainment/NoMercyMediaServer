using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoMercy.Storage.Drivers.WebDav;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for WebDAV folder drivers.
/// Supports Nextcloud, ownCloud, Synology DSM, SharePoint, and generic mod_dav servers.
/// </summary>
internal sealed record WebDavDriverConfig(
    string Url,
    string? Username,
    string? PasswordRef,
    string? BearerTokenRef,
    bool IgnoreCertErrors,
    int TimeoutSeconds
)
{
    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses and validates the JSON config blob from <c>Folder.DriverConfig</c>.
    /// Throws <see cref="ArgumentException"/> on missing or conflicting fields.
    /// </summary>
    internal static WebDavDriverConfig Parse(string json, Ulid folderId)
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

        bool hasBasicAuth =
            !string.IsNullOrWhiteSpace(raw.Username) || !string.IsNullOrWhiteSpace(raw.PasswordRef);
        bool hasBearerAuth = !string.IsNullOrWhiteSpace(raw.BearerTokenRef);

        if (hasBasicAuth && hasBearerAuth)
            throw new ArgumentException(
                $"driver_config for WebDAV folder {folderId} specifies both Basic Auth "
                    + "(username/passwordRef) and Bearer token (bearerTokenRef). "
                    + "Only one auth scheme may be set.",
                nameof(json)
            );

        int timeout = raw.TimeoutSeconds ?? 30;
        if (timeout <= 0)
            throw new ArgumentException(
                $"driver_config.timeoutSeconds must be positive for WebDAV folder {folderId} (got {timeout}).",
                nameof(json)
            );

        return new WebDavDriverConfig(
            NormalizeUrl(raw.Url.Trim()),
            raw.Username?.Trim(),
            raw.PasswordRef?.Trim(),
            raw.BearerTokenRef?.Trim(),
            raw.IgnoreCertErrors ?? false,
            timeout
        );
    }

    private static string NormalizeUrl(string url)
    {
        // Ensure the base URL ends with a slash so path joins work correctly.
        return url.TrimEnd('/') + "/";
    }

    // -----------------------------------------------------------------------
    // Overload for unit tests that supply individual fields without JSON
    // -----------------------------------------------------------------------
    internal static WebDavDriverConfig For(
        string url,
        string? username = null,
        string? passwordRef = null,
        string? bearerTokenRef = null,
        bool ignoreCertErrors = false,
        int timeoutSeconds = 30
    ) =>
        new(
            NormalizeUrl(url),
            username,
            passwordRef,
            bearerTokenRef,
            ignoreCertErrors,
            timeoutSeconds
        );

    // -----------------------------------------------------------------------
    // Raw deserialization target (case-insensitive keys)
    // -----------------------------------------------------------------------
    private sealed record WebDavDriverConfigRaw(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("passwordRef")] string? PasswordRef,
        [property: JsonPropertyName("bearerTokenRef")] string? BearerTokenRef,
        [property: JsonPropertyName("ignoreCertErrors")] bool? IgnoreCertErrors,
        [property: JsonPropertyName("timeoutSeconds")] int? TimeoutSeconds
    );
}
