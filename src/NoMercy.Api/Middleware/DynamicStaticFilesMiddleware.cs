using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using MimeMapping;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.Api.Middleware;

public class DynamicStaticFilesMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<Ulid, PhysicalFileProvider> Providers = new();

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

    public async Task InvokeAsync(HttpContext context, IStorage storage)
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
            if (
                !Ulid.TryParse(rootPath, out Ulid share)
                || !Providers.TryGetValue(share, out PhysicalFileProvider? provider)
            )
            {
                await next(context);
                return;
            }

            string? relativePath = pathValue?[pathValue.IndexOf('/', 1)..];
            IFileInfo? file = relativePath != null ? provider.GetFileInfo(relativePath) : null;

            if (file?.PhysicalPath != null)
                await ServeFile(context, file, storage);
            else
                await next(context);
        }
        catch (Exception ex)
        {
            Logger.App(
                $"DynamicStaticFilesMiddleware unhandled exception for path '{context.Request.Path}': {ex}"
            );
            throw;
        }
    }

    private static async Task ServeFile(HttpContext context, IFileInfo file, IStorage storage)
    {
        if (file.PhysicalPath is not { } filePhysicalPath)
            return;

        long fileLength = storage.Size(filePhysicalPath);

        context.Response.ContentType = MimeUtility.GetMimeMapping(file.PhysicalPath);

        bool isStreamableMedia = IsStreamableMedia(filePhysicalPath);
        bool hasRangeRequest = context.Request.Headers.TryGetValue(
            "Range",
            out StringValues rangeValue
        );

        // Force partial content for streamable media files or when range is requested
        if (!hasRangeRequest && !isStreamableMedia)
        {
            await context.Response.SendFileAsync(file.PhysicalPath);
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

        await using Stream fs = storage.OpenRead(file.PhysicalPath);

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

    public static void AddPath(Ulid requestPath, string physicalPath)
    {
        Providers[requestPath] = new(physicalPath);
    }

    public static void RemovePath(Ulid requestPath)
    {
        Providers.TryRemove(requestPath, out _);
    }
}
