using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using MimeMapping;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.Api.Middleware;

/// <summary>
/// Folder routing handle: maps a folder ULID to the driver instance + sub-path
/// the file lives under. Resolved per-request through IStorageFactory so NFS,
/// S3, WebDAV and local backends all stream through the same path.
/// </summary>
public readonly record struct FolderRef(Ulid DriverId, string SubPath);

public class DynamicStaticFilesMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<Ulid, FolderRef> Folders = new();

    // Define streamable media file extensions
    private static readonly HashSet<string> StreamableExtensions = new(
        StringComparer.OrdinalIgnoreCase
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

    public async Task InvokeAsync(HttpContext context, IStorageFactory storageFactory)
    {
        if (!context.Request.Path.HasValue)
        {
            await next(context);
            return;
        }

        string? pathValue = context.Request.Path.Value;
        string[] pathSegments = context
            .Request.Path.ToString()
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length == 0)
        {
            await next(context);
            return;
        }

        string rootPath = pathSegments[0];

        // Allow API endpoints, Swagger, and other system paths to pass through
        if (
            rootPath.Equals("api", StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals("index.html", StringComparison.OrdinalIgnoreCase)
            || rootPath.StartsWith("swagger", StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals("images", StringComparison.OrdinalIgnoreCase)
            || rootPath.Equals("manage", StringComparison.OrdinalIgnoreCase)
        )
        {
            await next(context);
            return;
        }

        try
        {
            if (!Ulid.TryParse(rootPath, out Ulid folderId))
            {
                await next(context);
                return;
            }

            if (!Folders.TryGetValue(folderId, out FolderRef folderRef))
            {
                Logger.App(
                    $"[DynamicStaticFiles] folder {folderId} not registered (request: {context.Request.Path})"
                );
                await next(context);
                return;
            }

            // Strip the leading "/<folderId>" segment to get the file's
            // sub-path within the folder. URL-decode + normalise to forward
            // slashes so storage drivers see a consistent shape.
            string relativeWithinFolder = pathValue is null
                ? string.Empty
                : Uri.UnescapeDataString(pathValue[pathValue.IndexOf('/', 1)..]).TrimStart('/');

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
                Logger.App(
                    $"[DynamicStaticFiles] factory.For failed for folder {folderId} driver {folderRef.DriverId} subPath '{folderRef.SubPath}': {fEx.Message}"
                );
                await next(context);
                return;
            }

            bool exists;
            try
            {
                exists = storage.Exists(relativeWithinFolder);
            }
            catch (Exception eEx)
            {
                Logger.App(
                    $"[DynamicStaticFiles] storage.Exists threw on '{relativeWithinFolder}' (folder {folderId}, driver {folderRef.DriverId}): {eEx.Message}"
                );
                await next(context);
                return;
            }

            if (!exists)
            {
                Logger.App(
                    $"[DynamicStaticFiles] not found: folder={folderId} driver={folderRef.DriverId} subPath='{folderRef.SubPath}' relative='{relativeWithinFolder}'"
                );
                await next(context);
                return;
            }

            Uri? presigned = await storage.TryGetPresignedUrlAsync(
                relativeWithinFolder,
                TimeSpan.FromHours(1),
                context.RequestAborted
            );
            if (presigned is not null)
            {
                context.Response.StatusCode = 302;
                context.Response.Headers.Location = presigned.ToString();
                return;
            }

            await ServeFile(context, storage, relativeWithinFolder);
        }
        catch (FileNotFoundException ex)
        {
            // Race: file vanished between Exists() and Size()/OpenRead().
            // Translate to 404 instead of an opaque 500.
            Logger.App(
                $"[DynamicStaticFiles] file vanished mid-serve for '{context.Request.Path}': {ex.Message}",
                Serilog.Events.LogEventLevel.Warning
            );
            if (!context.Response.HasStarted)
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }
        catch (IOException ex)
        {
            // Storage-layer failure (NFS hiccup, S3 throttling, disk error).
            Logger.App(
                $"[DynamicStaticFiles] storage IO failure for '{context.Request.Path}': {ex.Message}",
                Serilog.Events.LogEventLevel.Warning
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
            // Promote to Error so the stack trace reaches the log sink instead
            // of being filtered at default Information level.
            Logger.App(
                $"[DynamicStaticFiles] unhandled exception for path '{context.Request.Path}': {ex}",
                Serilog.Events.LogEventLevel.Error
            );
            throw;
        }
    }

    private static async Task ServeFile(HttpContext context, IStorage storage, string relativePath)
    {
        long fileLength = storage.Size(relativePath);

        context.Response.ContentType = MimeUtility.GetMimeMapping(relativePath);

        // Tell ResponseCachingMiddleware not to wrap the body. Without this
        // header it still allocates the cache stream wrapper around every
        // FLAC / video chunk we write — pointless overhead since media
        // responses are too large to ever cache (cap is 64 MB by default).
        context.Response.Headers.CacheControl = "no-store";

        bool isStreamableMedia = IsStreamableMedia(relativePath);
        bool hasRangeRequest = context.Request.Headers.TryGetValue(
            "Range",
            out StringValues rangeValue
        );

        // Force partial content for streamable media files or when range is requested.
        // For non-streamable + no range, stream the whole file via the storage facade.
        if (!hasRangeRequest && !isStreamableMedia)
        {
            context.Response.ContentLength = fileLength;
            await using Stream wholeStream = storage.OpenRead(relativePath);
            await wholeStream.CopyToAsync(context.Response.Body);
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
            string?[] ranges = rangeValue.ToString().Replace("bytes=", "").Split('-');

            if (!long.TryParse(ranges[0], out start))
            {
                context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
                    fileLength
                ).ToString();
                return;
            }

            if (ranges.Length > 1 && !string.IsNullOrEmpty(ranges[1]))
            {
                // Explicit end byte specified (e.g., "bytes=0-65535")
                if (!long.TryParse(ranges[1], out end))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
                        fileLength
                    ).ToString();
                    return;
                }
            }
            else if (isStreamableMedia && start == 0)
            {
                // Initial probe (browser asking "bytes=0-") — serve first chunk fast.
                end = Math.Min(start + initialProbeChunkSize - 1, fileLength - 1);
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
            end = Math.Min(start + initialProbeChunkSize - 1, fileLength - 1);
        }

        long length = end - start + 1;

        context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
        context.Response.Headers.ContentRange = new ContentRangeHeaderValue(
            start,
            end,
            fileLength
        ).ToString();
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentLength = length;

        await using Stream fs = storage.OpenRead(relativePath);

        fs.Seek(start, SeekOrigin.Begin);
        byte[] buffer = new byte[64 * 1024];
        int bytesRead;
        long bytesToRead = length;

        while (
            (bytesRead = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesToRead))) > 0
            && bytesToRead > 0
        )
        {
            await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
            bytesToRead -= bytesRead;
        }
    }

    private static bool IsStreamableMedia(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return StreamableExtensions.Contains(extension);
    }

    /// <summary>
    /// Register a folder for dynamic file serving. Pass the folder's ULID
    /// (becomes the URL root segment), the driver instance it belongs to,
    /// and its sub-path within that driver's root.
    /// </summary>
    public static void AddFolder(Ulid folderId, Ulid driverId, string subPath)
    {
        Folders[folderId] = new FolderRef(driverId, subPath ?? string.Empty);
    }

    public static void RemoveFolder(Ulid folderId)
    {
        Folders.TryRemove(folderId, out _);
    }
}
