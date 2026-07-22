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
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.App.EmbeddedStaticAssets;

/// <summary>
/// Middleware that serves static files from embedded resources with optimizations
/// similar to MapStaticAssets (caching headers, compression negotiation, ETags).
/// Supports HTML injection for scripts, styles, and meta tags.
/// </summary>
public sealed partial class EmbeddedStaticAssetsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ManifestEmbeddedFileProvider _fileProvider;
    private readonly EmbeddedStaticAssetsOptions _options;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;
    private readonly ConcurrentDictionary<string, CachedAsset> _assetCache;
    private readonly ILogger<EmbeddedStaticAssetsMiddleware> _logger;
    private readonly string _scriptsToInject;
    private readonly string _stylesToInject;
    private readonly string _metaTagsToInject;

    public EmbeddedStaticAssetsMiddleware(
        RequestDelegate next,
        ManifestEmbeddedFileProvider fileProvider,
        EmbeddedStaticAssetsOptions options,
        ILogger<EmbeddedStaticAssetsMiddleware> logger
    )
    {
        _next = next;
        _fileProvider = fileProvider;
        _options = options;
        _logger = logger;
        _contentTypeProvider = new();
        _assetCache = new(comparer: StringComparer.OrdinalIgnoreCase);

        // Pre-build injection strings
        _scriptsToInject = BuildScriptInjection(scripts: options.InjectScripts, minify: options.MinifyInjections);
        _stylesToInject = BuildStyleInjection(styles: options.InjectStyles, minify: options.MinifyInjections);
        _metaTagsToInject = BuildMetaTagInjection(metaTags: options.InjectMetaTags, minify: options.MinifyInjections);

        // Add additional MIME types
        _contentTypeProvider.Mappings[key: ".webmanifest"] = "application/manifest+json";
        _contentTypeProvider.Mappings[key: ".woff2"] = "font/woff2";
        _contentTypeProvider.Mappings[key: ".woff"] = "font/woff";
        _contentTypeProvider.Mappings[key: ".ttf"] = "font/ttf";
        _contentTypeProvider.Mappings[key: ".otf"] = "font/otf";
        _contentTypeProvider.Mappings[key: ".eot"] = "application/vnd.ms-fontobject";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value.OrEmpty();

        // Skip if not a GET or HEAD request
        if (
            !HttpMethods.IsGet(method: context.Request.Method)
            && !HttpMethods.IsHead(method: context.Request.Method)
        )
        {
            await _next(context: context);
            return;
        }

        // Normalize path - remove leading slash for file provider
        string filePath = path.TrimStart(trimChar: '/');
        if (string.IsNullOrEmpty(value: filePath))
        {
            filePath = "index.html";
        }

        // Try to get or create cached asset
        CachedAsset? asset = await GetOrCreateCachedAssetAsync(filePath: filePath);

        // SPA fallback: if no file found and request looks like a page navigation
        // (no file extension), serve index.html — the Vue router handles the route
        if (asset == null && !Path.HasExtension(path: filePath))
        {
            asset = await GetOrCreateCachedAssetAsync(filePath: "index.html");
        }

        if (asset == null)
        {
            await _next(context: context);
            return;
        }

        // Check for conditional request (If-None-Match)
        string requestETag = context.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(value: requestETag) && requestETag == asset.ETag)
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        // Determine best encoding based on Accept-Encoding header
        (byte[] content, string? encoding) = SelectBestEncoding(context: context, asset: asset);

        // Set response headers
        context.Response.ContentType = asset.ContentType;
        context.Response.ContentLength = content.Length;
        context.Response.Headers.ETag = asset.ETag;
        context.Response.Headers.LastModified = asset.LastModified.ToString(format: "R");

        // Set cache control based on whether file has a hash in its name
        if (HasContentHash(path: filePath))
        {
            // Immutable cache for fingerprinted assets
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else
        {
            // Short cache with revalidation for non-fingerprinted assets
            context.Response.Headers.CacheControl = "public, max-age=3600, must-revalidate";
        }

        if (!string.IsNullOrEmpty(value: encoding))
        {
            context.Response.Headers.ContentEncoding = encoding;
            context.Response.Headers.Vary = "Accept-Encoding";
        }

        // Write content for GET requests (not HEAD)
        if (HttpMethods.IsGet(method: context.Request.Method))
        {
            await context.Response.Body.WriteAsync(buffer: content);
        }
    }

    private async Task<CachedAsset?> GetOrCreateCachedAssetAsync(string filePath)
    {
        if (_assetCache.TryGetValue(key: filePath, value: out CachedAsset? cached))
        {
            return cached;
        }

        IFileInfo fileInfo = _fileProvider.GetFileInfo(subpath: filePath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        byte[] content;
        await using (Stream stream = fileInfo.CreateReadStream())
        using (MemoryStream ms = new())
        {
            await stream.CopyToAsync(destination: ms);
            content = ms.ToArray();
        }

        // Determine content type
        if (!_contentTypeProvider.TryGetContentType(subpath: filePath, contentType: out string? contentType))
        {
            contentType = "application/octet-stream";
        }

        // Apply HTML injection if this is an HTML file matching our patterns
        if (contentType == "text/html" && ShouldInjectHtml(filePath: filePath))
        {
            content = InjectHtmlContent(content: content);
            _logger.LogDebug(message: "Injected scripts/styles into: {Path}", args: filePath);
        }

        // Generate ETag from content hash (after injection)
        byte[] hashBytes = SHA256.HashData(source: content);
        string etag = $"\"{Convert.ToBase64String(inArray: hashBytes)}\"";

        // Pre-compress content
        byte[] gzipContent = CompressGzip(data: content);
        byte[] brotliContent = CompressBrotli(data: content);

        CachedAsset asset = new()
        {
            OriginalContent = content,
            GzipContent = gzipContent,
            BrotliContent = brotliContent,
            ContentType = contentType,
            ETag = etag,
            LastModified = fileInfo.LastModified.UtcDateTime,
        };

        _assetCache.TryAdd(key: filePath, value: asset);
        _logger.LogDebug(message: "Cached embedded asset: {Path} ({Size} bytes)", args: [filePath, content.Length]);

        return asset;
    }

    private bool ShouldInjectHtml(string filePath)
    {
        // Check if we have anything to inject
        if (
            string.IsNullOrEmpty(value: _scriptsToInject)
            && string.IsNullOrEmpty(value: _stylesToInject)
            && string.IsNullOrEmpty(value: _metaTagsToInject)
        )
        {
            return false;
        }

        // Check if file matches any of our patterns
        string fileName = Path.GetFileName(path: filePath);
        foreach (string pattern in _options.HtmlFilePatterns)
        {
            if (MatchesPattern(path: filePath, pattern: pattern) || MatchesPattern(path: fileName, pattern: pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPattern(string path, string pattern)
    {
        // Simple glob matching: * matches any characters, ** matches any path
        string regexPattern =
            "^" + Regex.Escape(str: pattern).Replace(oldValue: @"\*\*", newValue: ".*").Replace(oldValue: "\\*", newValue: "[^/]*") + "$";

        return Regex.IsMatch(input: path, pattern: regexPattern, options: RegexOptions.IgnoreCase);
    }

    private byte[] InjectHtmlContent(byte[] content)
    {
        string html = Encoding.UTF8.GetString(bytes: content);

        // Inject meta tags and styles before </head>
        if (!string.IsNullOrEmpty(value: _metaTagsToInject) || !string.IsNullOrEmpty(value: _stylesToInject))
        {
            string headInjection = _metaTagsToInject + _stylesToInject;
            html = HeadCloseRegex().Replace(input: html, replacement: headInjection + "</head>", count: 1);
        }

        // Inject scripts before </body>
        if (!string.IsNullOrEmpty(value: _scriptsToInject))
        {
            html = BodyCloseRegex().Replace(input: html, replacement: _scriptsToInject + "</body>", count: 1);
        }

        return Encoding.UTF8.GetBytes(s: html);
    }

    private static string BuildScriptInjection(List<string> scripts, bool minify)
    {
        if (scripts.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        foreach (string script in scripts)
        {
            if (script.TrimStart().StartsWith(value: "<script", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                // Already a complete script tag
                sb.Append(value: minify ? script.Trim() : script);
            }
            else
            {
                // Just a path, wrap in script tag
                sb.Append(handler: $"<script src=\"{script}\"></script>");
            }
        }
        return sb.ToString();
    }

    private static string BuildStyleInjection(List<string> styles, bool minify)
    {
        if (styles.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        foreach (string style in styles)
        {
            if (
                style.TrimStart().StartsWith(value: "<link", comparisonType: StringComparison.OrdinalIgnoreCase)
                || style.TrimStart().StartsWith(value: "<style", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            {
                // Already a complete tag
                sb.Append(value: minify ? style.Trim() : style);
            }
            else
            {
                // Just a path, wrap in link tag
                sb.Append(handler: $"<link rel=\"stylesheet\" href=\"{style}\">");
            }
        }
        return sb.ToString();
    }

    private static string BuildMetaTagInjection(List<string> metaTags, bool minify)
    {
        if (metaTags.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        foreach (string meta in metaTags)
        {
            sb.Append(value: minify ? meta.Trim() : meta);
        }
        return sb.ToString();
    }

    [GeneratedRegex(pattern: "</head>", options: RegexOptions.IgnoreCase)]
    private static partial Regex HeadCloseRegex();

    [GeneratedRegex(pattern: "</body>", options: RegexOptions.IgnoreCase)]
    private static partial Regex BodyCloseRegex();

    private static (byte[] content, string? encoding) SelectBestEncoding(
        HttpContext context,
        CachedAsset asset
    )
    {
        string acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();

        // Don't compress already compressed content types
        if (IsAlreadyCompressed(contentType: asset.ContentType))
        {
            return (asset.OriginalContent, null);
        }

        // Only compress if content is large enough to benefit
        if (asset.OriginalContent.Length < 1024)
        {
            return (asset.OriginalContent, null);
        }

        // Prefer Brotli over Gzip
        if (
            acceptEncoding.Contains(value: "br", comparisonType: StringComparison.OrdinalIgnoreCase)
            && asset.BrotliContent.Length < asset.OriginalContent.Length
        )
        {
            return (asset.BrotliContent, "br");
        }

        if (
            acceptEncoding.Contains(value: "gzip", comparisonType: StringComparison.OrdinalIgnoreCase)
            && asset.GzipContent.Length < asset.OriginalContent.Length
        )
        {
            return (asset.GzipContent, "gzip");
        }

        return (asset.OriginalContent, null);
    }

    private static bool IsAlreadyCompressed(string contentType)
    {
        return contentType.StartsWith(value: "image/", comparisonType: StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith(value: "video/", comparisonType: StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith(value: "audio/", comparisonType: StringComparison.OrdinalIgnoreCase)
            || contentType.Contains(value: "zip", comparisonType: StringComparison.OrdinalIgnoreCase)
            || contentType.Contains(value: "compressed", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasContentHash(string path)
    {
        // Check for common fingerprint patterns like file-abc123.js or file.abc123.js
        string fileName = Path.GetFileNameWithoutExtension(path: path);
        string extension = Path.GetExtension(path: path);

        // Pattern: name-hash.ext (e.g., app-DxhB2PJG.js)
        int lastDash = fileName.LastIndexOf(value: '-');
        if (lastDash > 0 && lastDash < fileName.Length - 1)
        {
            string potentialHash = fileName[(lastDash + 1)..];
            if (
                potentialHash.Length >= 6
                && potentialHash.All(predicate: c => char.IsLetterOrDigit(c: c) || c == '_')
            )
            {
                return true;
            }
        }

        // Pattern: name.hash.ext (e.g., workbox-f456e5ee.js)
        int lastDot = fileName.LastIndexOf(value: '.');
        if (lastDot > 0 && lastDot < fileName.Length - 1)
        {
            string potentialHash = fileName[(lastDot + 1)..];
            if (
                potentialHash.Length >= 6
                && potentialHash.All(predicate: c => char.IsLetterOrDigit(c: c) || c == '_')
            )
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] CompressGzip(byte[] data)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(stream: output, compressionLevel: CompressionLevel.SmallestSize))
        {
            gzip.Write(buffer: data, offset: 0, count: data.Length);
        }
        return output.ToArray();
    }

    private static byte[] CompressBrotli(byte[] data)
    {
        using MemoryStream output = new();
        using (BrotliStream brotli = new(stream: output, compressionLevel: CompressionLevel.SmallestSize))
        {
            brotli.Write(buffer: data, offset: 0, count: data.Length);
        }
        return output.ToArray();
    }

    private sealed class CachedAsset
    {
        public required byte[] OriginalContent { get; init; }
        public required byte[] GzipContent { get; init; }
        public required byte[] BrotliContent { get; init; }
        public required string ContentType { get; init; }
        public required string ETag { get; init; }
        public required DateTime LastModified { get; init; }
    }
}
