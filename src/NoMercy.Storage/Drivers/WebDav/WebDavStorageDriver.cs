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

using System.Net;
using NoMercy.Storage.Common;
using WebDav;

namespace NoMercy.Storage.Drivers.WebDav;

/// <summary>
/// <see cref="IStorageDriver"/> backed by any RFC 4918-compliant WebDAV server
/// (Nextcloud, ownCloud, Synology DSM, SharePoint, generic mod_dav, …).
/// </summary>
public sealed class WebDavStorageDriver : IStorageDriver, IDisposable
{
    public string BackendLabel => "WebDAV";

    private readonly IWebDavClient _client;
    private readonly string _baseUrl;

    // Dispose tracking — we only own the client when we created it.
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>
    /// Production constructor — builds an <see cref="IWebDavClient"/> from config.
    /// Internal because <see cref="WebDavDriverConfig"/> is internal; callers use
    /// <see cref="NoMercy.Storage.Factory.StorageFactory"/> instead.
    /// Credentials (username / password) must already be set on the config by the factory
    /// before this constructor is called.
    /// </summary>
    internal WebDavStorageDriver(WebDavDriverConfig config)
    {
        ArgumentNullException.ThrowIfNull(argument: config);

        _baseUrl = config.Url; // already normalized with trailing slash

        HttpClientHandler handler = new();

        if (config.IgnoreCertErrors)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        ICredentials? credentials = BuildCredentials(username: config.Username, password: config.Password);
        if (credentials is not null)
            handler.Credentials = credentials;

        HttpClient httpClient = new(handler: handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(seconds: config.TimeoutSeconds),
        };

        // Pass the pre-configured HttpClient directly so our handler + auth are used.
        _client = new WebDavClient(httpClient: httpClient);
        _ownsClient = true;
    }

    /// <summary>
    /// Test constructor — injects a pre-configured client (e.g. pointing at
    /// a Testcontainers WebDAV instance) and a fixed base URL.
    /// </summary>
    internal WebDavStorageDriver(IWebDavClient client, string baseUrl)
    {
        _client = client ?? throw new ArgumentNullException(paramName: nameof(client));
        _baseUrl = baseUrl.TrimEnd(trimChar: '/') + "/";
        _ownsClient = false;
    }

    // -----------------------------------------------------------------------
    // IStorageDriver implementation
    // -----------------------------------------------------------------------

    public bool FileExists(string path)
    {
        string uri = ToUri(path: path);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceOnly })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            return false;

        WebDavResource? resource = response.Resources.FirstOrDefault();
        // A 207 with a collection resource = directory, not a file.
        return resource is not null && !resource.IsCollection;
    }

    public bool DirectoryExists(string path)
    {
        string uri = ToCollectionUri(path: path);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceOnly })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            return false;

        WebDavResource? resource = response.Resources.FirstOrDefault();
        return resource is not null && resource.IsCollection;
    }

    public void CreateDirectory(string path)
    {
        // WebDAV MKCOL is not recursive — create each missing segment in order.
        string normalized = path.TrimStart(trimChar: '/').TrimStart(trimChar: '\\').Replace(oldChar: '\\', newChar: '/');
        string[] segments = normalized.Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries);

        string accumulated = string.Empty;
        foreach (string segment in segments)
        {
            accumulated = string.IsNullOrEmpty(value: accumulated) ? segment : accumulated + "/" + segment;

            string uri = ToCollectionUri(path: accumulated);

            if (DirectoryExists(path: accumulated))
                continue;

            WebDavResponse response = _client.Mkcol(requestUri: uri).GetAwaiter().GetResult();

            // 405 Method Not Allowed = already exists (some servers respond this way).
            if (!response.IsSuccessful && response.StatusCode != 405)
                throw new IOException(
                    message: $"WebDAV MKCOL '{uri}' failed: HTTP {response.StatusCode} — {response.Description}"
                );
        }
    }

    public void DeleteFile(string path)
    {
        string uri = ToUri(path: path);
        WebDavResponse response = _client.Delete(requestUri: uri).GetAwaiter().GetResult();

        // 404 is idempotent — file was already gone.
        if (!response.IsSuccessful && response.StatusCode != 404)
            throw new IOException(
                message: $"WebDAV DELETE '{uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        string uri = ToCollectionUri(path: path);

        if (!recursive)
        {
            // Reject if the collection is non-empty.
            PropfindResponse listing = _client
                .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceAndChildren })
                .GetAwaiter()
                .GetResult();

            if (listing is { IsSuccessful: true, Resources.Count: > 1 })
                throw new IOException(
                    message: $"Cannot delete non-empty directory '{path}' with recursive=false."
                );
        }

        WebDavResponse response = _client.Delete(requestUri: uri).GetAwaiter().GetResult();

        if (!response.IsSuccessful && response.StatusCode != 404)
            throw new IOException(
                message: $"WebDAV DELETE '{uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
    }

    public long GetFileSize(string path)
    {
        WebDavResource resource = PropfindSingle(path: path);
        return resource.ContentLength ?? 0L;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        WebDavResource resource = PropfindSingle(path: path);
        return resource.LastModifiedDate?.ToUniversalTime() ?? DateTime.UtcNow;
    }

    // WebDAV does not expose ctime or atime — return LastModified as the closest equivalent.
    public DateTime GetCreationTimeUtc(string path) => GetLastWriteTimeUtc(path: path);

    public DateTime GetLastAccessTimeUtc(string path) => GetLastWriteTimeUtc(path: path);

    public Stream OpenRead(string path)
    {
        string uri = ToUri(path: path);
        WebDavStreamResponse response = _client.GetRawFile(requestUri: uri).GetAwaiter().GetResult();

        if (!response.IsSuccessful)
            throw new IOException(
                message: $"WebDAV GET '{uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );

        return response.Stream;
    }

    public Stream OpenWrite(string path, bool overwrite)
    {
        // WebDAV requires every parent collection to exist before the PUT;
        // otherwise the server replies 403/409.
        EnsureParentCollection(path: path);

        string uri = ToUri(path: path);
        return new WebDavWriteStream(client: _client, uri: uri, overwrite: overwrite);
    }

    /// <summary>
    /// MKCOL each missing segment of the destination's parent collection.
    /// CreateDirectory is idempotent (treats 405 as already-exists) so this
    /// is safe to call unconditionally before any PUT/MOVE/COPY.
    /// </summary>
    private void EnsureParentCollection(string path)
    {
        string normalized = path.TrimStart(trimChar: '/').TrimStart(trimChar: '\\').Replace(oldChar: '\\', newChar: '/');
        int lastSlash = normalized.LastIndexOf(value: '/');
        if (lastSlash > 0)
            CreateDirectory(path: normalized[..lastSlash]);
    }

    public void MoveFile(string source, string destination)
    {
        // Same parent-collection requirement as PUT — destination's parent
        // must exist or the server returns 409 Conflict.
        EnsureParentCollection(path: destination);

        string srcUri = ToUri(path: source);
        string dstUri = ToUri(path: destination);

        WebDavResponse response = _client
            .Move(sourceUri: srcUri, destUri: dstUri, parameters: new() { Overwrite = true })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            throw new IOException(
                message: $"WebDAV MOVE '{srcUri}' → '{dstUri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
    }

    public void CopyFile(string source, string destination, bool overwrite)
    {
        EnsureParentCollection(path: destination);

        string srcUri = ToUri(path: source);
        string dstUri = ToUri(path: destination);

        WebDavResponse response = _client
            .Copy(sourceUri: srcUri, destUri: dstUri, parameters: new() { Overwrite = overwrite })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            throw new IOException(
                message: $"WebDAV COPY '{srcUri}' → '{dstUri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
    }

    /// <summary>
    /// Batched listing — walks PROPFIND results once and emits StorageEntryInfo
    /// with Size + LastWriteUtc populated from the response. Overrides the
    /// IStorageDriver default which would issue N×PROPFIND per entry to fill
    /// the same metadata, hammering the server and tripping auth quirks on
    /// some WebDAV implementations.
    /// </summary>
    public IEnumerable<StorageEntryInfo> EnumerateEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        return EnumerateRecursiveWithMeta(
            directory: directory,
            searchPattern: searchPattern,
            recursive: option == SearchOption.AllDirectories
        );
    }

    private IEnumerable<StorageEntryInfo> EnumerateRecursiveWithMeta(
        string directory,
        string searchPattern,
        bool recursive
    )
    {
        string uri = ToCollectionUri(path: directory);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceAndChildren })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
        {
            if (response.StatusCode == 404)
                yield break;
            throw new IOException(
                message: $"WebDAV PROPFIND '{uri}' failed: HTTP {response.StatusCode} — "
                         + $"{response.Description ?? "(no description)"}"
            );
        }

        // Resources[0] is the directory itself — skip it.
        foreach (WebDavResource resource in response.Resources.Skip(count: 1))
        {
            string resourceUri =
                resource.Uri
                ?? throw new IOException(
                    message: $"WebDAV PROPFIND '{uri}' returned a resource with no URI."
                );
            string entryName = ExtractName(uri: resourceUri);

            if (resource.IsCollection)
            {
                string relPath = MakeRelative(absoluteUri: resourceUri).TrimEnd(trimChar: '/');
                if (StoragePatternMatcher.Matches(name: entryName, pattern: searchPattern))
                    yield return new(
                        Path: relPath,
                        IsDirectory: true,
                        Size: 0L,
                        LastWriteUtc: resource.LastModifiedDate?.ToUniversalTime()
                            ?? DateTime.UtcNow
                    );

                if (recursive)
                {
                    foreach (
                        StorageEntryInfo child in EnumerateRecursiveWithMeta(
                            directory: relPath,
                            searchPattern: searchPattern,
                            recursive: true
                        )
                    )
                        yield return child;
                }
            }
            else
            {
                if (StoragePatternMatcher.Matches(name: entryName, pattern: searchPattern))
                    yield return new(
                        Path: MakeRelative(absoluteUri: resourceUri),
                        IsDirectory: false,
                        Size: resource.ContentLength ?? 0L,
                        LastWriteUtc: resource.LastModifiedDate?.ToUniversalTime()
                            ?? DateTime.UtcNow
                    );
            }
        }
    }

    public IEnumerable<string> EnumerateFileSystemEntries(
        string directory,
        string searchPattern,
        SearchOption option
    )
    {
        return EnumerateRecursive(directory: directory, searchPattern: searchPattern, recursive: option == SearchOption.AllDirectories);
    }

    public string GetFullPath(string path)
    {
        // Pure URL normalization — no filesystem touch.
        string normalized = path.TrimStart(trimChar: '/').TrimStart(trimChar: '\\').Replace(oldChar: '\\', newChar: '/');
        if (string.IsNullOrEmpty(value: normalized))
            return _baseUrl.TrimEnd(trimChar: '/');
        return _baseUrl.TrimEnd(trimChar: '/') + "/" + Uri.EscapeDataString(stringToEscape: normalized).Replace(oldValue: "%2F", newValue: "/");
    }

    public string? ResolveLinkTarget(string path) => null;

    public bool IsHidden(string path)
    {
        string uri = ToUri(path: path);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceOnly })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            return false;

        WebDavResource? resource = response.Resources.FirstOrDefault();
        return resource?.IsHidden ?? false;
    }

    public void MoveDirectory(string source, string destination)
    {
        string srcUri = ToCollectionUri(path: source);
        string dstUri = ToCollectionUri(path: destination);

        WebDavResponse response = _client
            .Move(sourceUri: srcUri, destUri: dstUri, parameters: new() { Overwrite = true })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            throw new IOException(
                message: $"WebDAV MOVE dir '{srcUri}' → '{dstUri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_ownsClient && _client is IDisposable disposable)
            disposable.Dispose();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Builds the full URI for a file-like path (no trailing slash).</summary>
    private string ToUri(string path)
    {
        string normalized = path.TrimStart(trimChar: '/').TrimStart(trimChar: '\\').Replace(oldChar: '\\', newChar: '/');
        return _baseUrl.TrimEnd(trimChar: '/') + "/" + normalized;
    }

    /// <summary>Builds the full URI for a collection path (trailing slash).</summary>
    private string ToCollectionUri(string path)
    {
        string normalized = path.TrimStart(trimChar: '/').TrimStart(trimChar: '\\').Replace(oldChar: '\\', newChar: '/').TrimEnd(trimChar: '/');
        if (string.IsNullOrEmpty(value: normalized))
            return _baseUrl;
        return _baseUrl.TrimEnd(trimChar: '/') + "/" + normalized + "/";
    }

    /// <summary>PROPFIND Depth:0 and return the single resource, or throw.</summary>
    private WebDavResource PropfindSingle(string path)
    {
        string uri = ToUri(path: path);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceOnly })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
            throw new IOException(
                message: $"WebDAV PROPFIND '{uri}' failed: HTTP {response.StatusCode} — {response.Description}"
            );

        WebDavResource? resource = response.Resources.FirstOrDefault();
        if (resource is null)
            throw new FileNotFoundException(message: $"WebDAV resource not found: {path}");

        return resource;
    }

    private IEnumerable<string> EnumerateRecursive(
        string directory,
        string searchPattern,
        bool recursive
    )
    {
        string uri = ToCollectionUri(path: directory);
        PropfindResponse response = _client
            .Propfind(requestUri: uri, parameters: new() { ApplyTo = ApplyTo.Propfind.ResourceAndChildren })
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessful)
        {
            // Path Contract: List on a non-existent directory returns empty,
            // never throws. 404 from the WebDAV server means the collection
            // doesn't exist — yield nothing. Auth/path failures still surface
            // so operators can act on them.
            if (response.StatusCode == 404)
                yield break;

            throw new IOException(
                message: $"WebDAV PROPFIND '{uri}' failed: HTTP {response.StatusCode} — "
                         + $"{response.Description ?? "(no description)"}. "
                         + (
                             response.StatusCode == 401 || response.StatusCode == 403
                                 ? "Check the driver credentials in the dashboard."
                                 : "Check the driver URL and that the server speaks WebDAV at this path."
                         )
            );
        }

        // Resources[0] is the directory itself — skip it.
        foreach (WebDavResource resource in response.Resources.Skip(count: 1))
        {
            string resourceUri =
                resource.Uri
                ?? throw new IOException(
                    message: $"WebDAV PROPFIND '{uri}' returned a resource with no URI."
                );
            string entryName = ExtractName(uri: resourceUri);

            if (resource.IsCollection)
            {
                // Strip trailing slash so consumers get a consistent basename via LastIndexOf('/').
                string relPath = MakeRelative(absoluteUri: resourceUri).TrimEnd(trimChar: '/');
                if (StoragePatternMatcher.Matches(name: entryName, pattern: searchPattern))
                    yield return relPath;

                if (recursive)
                {
                    foreach (string child in EnumerateRecursive(directory: relPath, searchPattern: searchPattern, recursive: true))
                        yield return child;
                }
            }
            else
            {
                if (StoragePatternMatcher.Matches(name: entryName, pattern: searchPattern))
                    yield return MakeRelative(absoluteUri: resourceUri);
            }
        }
    }

    private string MakeRelative(string absoluteUri)
    {
        if (absoluteUri.StartsWith(value: _baseUrl, comparisonType: StringComparison.OrdinalIgnoreCase))
            return absoluteUri.Substring(startIndex: _baseUrl.Length);

        // Fall back to stripping the scheme+host prefix.
        Uri parsed = new(uriString: absoluteUri, uriKind: UriKind.RelativeOrAbsolute);
        return parsed.IsAbsoluteUri
            ? parsed.PathAndQuery.TrimStart(trimChar: '/')
            : absoluteUri.TrimStart(trimChar: '/');
    }

    private static string ExtractName(string uri)
    {
        string trimmed = uri.TrimEnd(trimChar: '/');
        int lastSlash = trimmed.LastIndexOf(value: '/');
        return lastSlash >= 0 ? trimmed.Substring(startIndex: lastSlash + 1) : trimmed;
    }

    // -----------------------------------------------------------------------
    // Credential helpers
    // -----------------------------------------------------------------------

    private static ICredentials? BuildCredentials(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(value: username) && string.IsNullOrWhiteSpace(value: password))
            return null;

        return new NetworkCredential(userName: username ?? string.Empty, password: password ?? string.Empty);
    }
}
