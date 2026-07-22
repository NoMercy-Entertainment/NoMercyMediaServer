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
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using MimeMapping;
using NoMercy.NmSystem.Monitoring;
using NoMercy.Storage;

namespace NoMercy.Api.Middleware;

/// <summary>
/// Folder routing handle: maps a folder ULID to the driver instance + sub-path
/// the file lives under. Resolved per-request through IStorageFactory so NFS,
/// S3, WebDAV and local backends all stream through the same path.
/// </summary>
public readonly record struct FolderRef(Ulid DriverId, string SubPath);

public class DynamicStaticFilesMiddleware(
    RequestDelegate next,
    ILogger<DynamicStaticFilesMiddleware> logger
)
{
    private static readonly ConcurrentDictionary<Ulid, FolderRef> Folders = new();

    // Define streamable media file extensions
    private static readonly HashSet<string> StreamableExtensions = new(
        comparer: StringComparer.OrdinalIgnoreCase
    )
    {
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".wmv",
        ".flv",
        ".webm",
        ".m4v",
        ".3gp",
        ".ogv",
        ".mp3",
        ".aac",
        ".flac",
        ".ogg",
        ".wav",
        ".wma",
        ".m4a",
        ".opus",
    };

    public async Task InvokeAsync(
        HttpContext context,
        IStorageFactory storageFactory,
        MediaActivityMonitor activityMonitor
    )
    {
        if (!context.Request.Path.HasValue)
        {
            await next(context: context);
            return;
        }

        string? pathValue = context.Request.Path.Value;
        string[] pathSegments = context
            .Request.Path.ToString()
            .Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length == 0)
        {
            await next(context: context);
            return;
        }

        string rootPath = pathSegments[0];

        // Allow API endpoints, Swagger, and other system paths to pass through
        if (
            rootPath.Equals(value: "api", comparisonType: StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals(value: "index.html", comparisonType: StringComparison.OrdinalIgnoreCase)
            || rootPath.StartsWith(value: "swagger", comparisonType: StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals(value: "images", comparisonType: StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals(value: "manage", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            await next(context: context);
            return;
        }

        try
        {
            if (!Ulid.TryParse(base32: rootPath, ulid: out Ulid folderId))
            {
                await next(context: context);
                return;
            }

            if (!Folders.TryGetValue(key: folderId, value: out FolderRef folderRef))
            {
                logger.LogInformation(
                    message: "[DynamicStaticFiles] folder {FolderId} not registered (request: {Path})", args: [folderId, context.Request.Path]
                );
                await next(context: context);
                return;
            }

            // Strip the leading "/<folderId>" segment to get the file's
            // sub-path within the folder. URL-decode + normalise to forward
            // slashes so storage drivers see a consistent shape.
            string relativeWithinFolder = pathValue is null
                ? string.Empty
                : Uri.UnescapeDataString(stringToUnescape: pathValue[pathValue.IndexOf(value: '/', startIndex: 1)..]).TrimStart(trimChar: '/');

            // Per-request server-side timing for media serves. Audio/video file
            // requests bypass AccessLogMiddleware, so without this they have zero
            // timing visibility. Logs how long the server itself spends resolving
            // + opening + streaming the file — isolating "server slow" from
            // client-side connect/DNS/TLS latency.
            Stopwatch stopwatch = Stopwatch.StartNew();
            long resolvedAtMs = 0;

            IStorage storage;
            try
            {
                storage = storageFactory.For(
                    folderId: folderId,
                    driverId: folderRef.DriverId,
                    subPath: folderRef.SubPath
                );
            }
            catch (Exception fEx)
            {
                logger.LogInformation(
                    message: "[DynamicStaticFiles] factory.For failed for folder {FolderId} driver {DriverId} subPath '{SubPath}': {Message}", args: [folderId, folderRef.DriverId, folderRef.SubPath, fEx.Message]
                );
                await next(context: context);
                return;
            }

            bool exists;
            try
            {
                exists = storage.Exists(path: relativeWithinFolder);
            }
            catch (Exception eEx)
            {
                logger.LogInformation(
                    message: "[DynamicStaticFiles] storage.Exists threw on '{RelativeWithinFolder}' (folder {FolderId}, driver {DriverId}): {Message}", args: [relativeWithinFolder, folderId, folderRef.DriverId, eEx.Message]
                );
                await next(context: context);
                return;
            }

            if (!exists)
            {
                logger.LogInformation(
                    message: "[DynamicStaticFiles] not found: folder={FolderId} driver={DriverId} subPath='{SubPath}' relative='{RelativeWithinFolder}'", args: [folderId, folderRef.DriverId, folderRef.SubPath, relativeWithinFolder]
                );
                await next(context: context);
                return;
            }

            Uri? presigned = await storage.TryGetPresignedUrlAsync(
                path: relativeWithinFolder,
                ttl: TimeSpan.FromHours(hours: 1),
                ct: context.RequestAborted
            );
            if (presigned is not null)
            {
                context.Response.StatusCode = 302;
                context.Response.Headers.Location = presigned.ToString();
                return;
            }

            // Every playlist/segment/subtitle fetch counts as playback activity —
            // background NAS-heavy jobs (scans/imports/extras) defer while this
            // keeps landing, see MediaPlaybackActivityGate.
            activityMonitor.Touch();

            // Time-to-first-byte on the server: everything before the stream loop
            // (factory resolve + Exists + presigned probe + Size + OpenRead).
            resolvedAtMs = stopwatch.ElapsedMilliseconds;
            await ServeFile(context: context, storage: storage, relativePath: relativeWithinFolder);
            stopwatch.Stop();

            if (resolvedAtMs > 1000 || stopwatch.ElapsedMilliseconds > 2000)
                logger.LogWarning(
                    message: "[DynamicStaticFiles] SLOW serve '{RelativeWithinFolder}' prep={ResolvedAtMs}ms total={ElapsedMilliseconds}ms (driver={Name})", args: [relativeWithinFolder, resolvedAtMs, stopwatch.ElapsedMilliseconds, storage.GetType().Name]
                );
            else
                logger.LogDebug(
                    message: "[DynamicStaticFiles] serve '{RelativeWithinFolder}' prep={ResolvedAtMs}ms total={ElapsedMilliseconds}ms", args: [relativeWithinFolder, resolvedAtMs, stopwatch.ElapsedMilliseconds]
                );
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Race: file or its containing directory vanished between Exists()
            // and Size()/OpenRead(). Translate to 404 instead of an opaque 500.
            logger.LogWarning(
                message: "[DynamicStaticFiles] file vanished mid-serve for '{Path}': {Message}", args: [context.Request.Path, ex.Message]
            );
            if (!context.Response.HasStarted)
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            // Storage-layer transport failure (NFS hiccup, S3 / WebDAV 5xx, disk
            // error). 502 reflects "we couldn't reach the backend that holds
            // this file" — distinct from "the file doesn't exist."
            logger.LogWarning(
                message: "[DynamicStaticFiles] storage transport failure for '{Path}': {Message}", args: [context.Request.Path, ex.Message]
            );
            if (!context.Response.HasStarted)
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected mid-stream — no response to send.
        }
        catch (Exception ex)
        {
            // Anything else escaping ServeFile is treated as a backend fault,
            // not a server bug — surface as 502 so ExoPlayer / browser fall
            // through to the next track gracefully instead of crashing on a
            // 500 with a stack trace body. Logged at Error so genuine bugs
            // remain visible in the sink.
            logger.LogError(
                message: "[DynamicStaticFiles] unhandled exception for path '{Path}': {Ex}", args: [context.Request.Path, ex]
            );
            if (!context.Response.HasStarted)
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
    }

    private async Task ServeFile(HttpContext context, IStorage storage, string relativePath)
    {
        long fileLength = storage.Size(path: relativePath);

        // Surface storage-reported zero — empty bodies on m3u8 / vtt /
        // fonts.json requests almost always trace back to either an encoder
        // that hasn't flushed yet or an NFS metadata cache lying. Logging it
        // here narrows triage in one step instead of guessing.
        if (fileLength == 0)
        {
            logger.LogWarning(
                message: "[DynamicStaticFiles] storage reports 0 bytes for '{Path}' (driver={Name})", args: [context.Request.Path, storage.GetType().Name]
            );
        }

        context.Response.ContentType = ResolveContentType(filePath: relativePath);

        // Tell ResponseCachingMiddleware not to wrap the body. Without this
        // header it still allocates the cache stream wrapper around every
        // FLAC / video chunk we write — pointless overhead since media
        // responses are too large to ever cache (cap is 64 MB by default).
        context.Response.Headers.CacheControl = "no-store";

        bool isStreamableMedia = IsStreamableMedia(filePath: relativePath);
        bool hasRangeRequest = context.Request.Headers.TryGetValue(
            key: "Range",
            value: out StringValues rangeValue
        );

        // Force partial content for streamable media files or when range is requested.
        // For non-streamable + no range, stream the whole file via the storage facade.
        if (!hasRangeRequest && !isStreamableMedia)
        {
            context.Response.ContentLength = fileLength;
            await using Stream wholeStream = storage.OpenRead(path: relativePath);
            await wholeStream.CopyToAsync(destination: context.Response.Body);
            return;
        }

        // Parse range or default to start of file for streamable media
        long start = 0;
        long end;

        // Initial probe chunk size (1 MB) — serves the first slice fast for browsers
        // that issue a "bytes=0-" or no-range request, so they can start parsing the
        // moov atom without waiting on the whole file. Any other open-ended range
        // (start > 0) is served to EOF: ExoPlayer's DefaultExtractorInput reads
        // sequentially via Mp4Extractor.readFully, and capping at 1 MiB makes its
        // read return -1 mid-atom and throws EOFException (web's <video> reopens
        // the connection automatically; ExoPlayer does not).
        const long initialProbeChunkSize = 1024 * 1024;

        if (hasRangeRequest)
        {
            string?[] ranges = rangeValue.ToString().Replace(oldValue: "bytes=", newValue: "").Split(separator: '-');

            if (!long.TryParse(s: ranges[0], result: out start))
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
                    length: fileLength
                ).ToString();
                return;
            }

            if (ranges.Length > 1 && !string.IsNullOrEmpty(value: ranges[1]))
            {
                // Explicit end byte specified (e.g., "bytes=0-65535")
                if (!long.TryParse(s: ranges[1], result: out end))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
                        length: fileLength
                    ).ToString();
                    return;
                }
            }
            else if (isStreamableMedia && start == 0)
            {
                // Initial probe (browser asking "bytes=0-") — serve first chunk fast.
                end = Math.Min(val1: start + initialProbeChunkSize - 1, val2: fileLength - 1);
            }
            else
            {
                // Open-ended range with non-zero start, or non-streamable file —
                // serve everything from start to EOF. Required for ExoPlayer's
                // sequential readFully across MP4 atoms.
                end = fileLength - 1;
            }
        }
        else
        {
            // Streamable media without range request — serve initial chunk so the
            // browser can start playback before the full file streams in.
            end = Math.Min(val1: start + initialProbeChunkSize - 1, val2: fileLength - 1);
        }

        // Clamp an explicit end that runs past EOF, then reject any range that is
        // still unsatisfiable. ContentRangeHeaderValue's ctor throws
        // ArgumentOutOfRangeException on start<0 or start>end — a zero-length segment
        // (end becomes fileLength-1 = -1) or a start seeked at/after the segment's EOF.
        // Without this it surfaced as an unhandled 500 the player retried in a tight
        // loop (spamming [DynamicStaticFiles] exceptions for one bad .m4s segment).
        if (end > fileLength - 1)
            end = fileLength - 1;

        if (start < 0 || start > end)
        {
            context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
                length: fileLength
            ).ToString();
            return;
        }

        long length = end - start + 1;

        context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
        context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
            from: start,
            to: end,
            length: fileLength
        ).ToString();
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentLength = length;

        await using Stream fs = storage.OpenRead(path: relativePath);

        fs.Seek(offset: start, origin: SeekOrigin.Begin);
        byte[] buffer = new byte[64 * 1024];
        int bytesRead;
        long bytesToRead = length;

        while (bytesToRead > 0)
        {
            bytesRead = await fs.ReadAsync(
                buffer: buffer.AsMemory(start: 0, length: (int)Math.Min(val1: buffer.Length, val2: bytesToRead)),
                cancellationToken: context.RequestAborted
            );
            if (bytesRead == 0)
                break;

            await context.Response.Body.WriteAsync(
                buffer: buffer.AsMemory(start: 0, length: bytesRead),
                cancellationToken: context.RequestAborted
            );
            bytesToRead -= bytesRead;
        }
    }

    private static bool IsStreamableMedia(string filePath)
    {
        string extension = Path.GetExtension(path: filePath);
        return StreamableExtensions.Contains(item: extension);
    }

    // MimeMapping (the NuGet package) doesn't know about subtitle/font/HLS
    // formats and defaults them to application/octet-stream — which the
    // browser refuses to render as text. Override the handful that matter
    // and fall back to the library for everything else.
    private static readonly Dictionary<string, string> ContentTypeOverrides = new(
        comparer: StringComparer.OrdinalIgnoreCase
    )
    {
        // Subtitles
        [key: ".ass"] = "text/x-ssa; charset=utf-8",
        [key: ".ssa"] = "text/x-ssa; charset=utf-8",
        [key: ".srt"] = "application/x-subrip; charset=utf-8",
        [key: ".vtt"] = "text/vtt; charset=utf-8",
        [key: ".sub"] = "text/plain; charset=utf-8",
        [key: ".idx"] = "text/plain; charset=utf-8",
        [key: ".sup"] = "application/octet-stream",
        // HLS
        [key: ".m3u8"] = "application/vnd.apple.mpegurl",
        [key: ".m3u"] = "application/vnd.apple.mpegurl",
        [key: ".ts"] = "video/mp2t",
        // Fonts (encoder-extracted attachments)
        [key: ".otf"] = "font/otf",
        [key: ".ttf"] = "font/ttf",
        [key: ".woff"] = "font/woff",
        [key: ".woff2"] = "font/woff2",
    };

    private static string ResolveContentType(string filePath)
    {
        string ext = Path.GetExtension(path: filePath);
        if (ContentTypeOverrides.TryGetValue(key: ext, value: out string? mapped))
            return mapped;
        return MimeUtility.GetMimeMapping(file: filePath);
    }

    /// <summary>
    /// Register a folder for dynamic file serving. Pass the folder's ULID
    /// (becomes the URL root segment), the driver instance it belongs to,
    /// and its sub-path within that driver's root.
    /// </summary>
    public static void AddFolder(Ulid folderId, Ulid driverId, string subPath)
    {
        Folders[key: folderId] = new(DriverId: driverId, SubPath: subPath ?? string.Empty);
    }

    public static void RemoveFolder(Ulid folderId)
    {
        Folders.TryRemove(key: folderId, value: out _);
    }
}
