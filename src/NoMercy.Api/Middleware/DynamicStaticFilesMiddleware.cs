using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NoMercy.Encoder.Format.Rules;

namespace NoMercy.Api.Middleware;

public class DynamicStaticFilesMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<Ulid, PhysicalFileProvider> Providers = new();
    
    // Define streamable media file extensions
    private static readonly HashSet<string> StreamableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".ogv",
        ".mp3", ".aac", ".flac", ".ogg", ".wav", ".wma", ".m4a", ".opus"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.HasValue)
        {
            await next(context);
            return;
        }

        string? pathValue = context.Request.Path.Value;
        string[] pathSegments = context.Request.Path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (pathSegments.Length == 0)
        {
            await next(context);
            return;
        }
        
        string rootPath = pathSegments[0];
        
        // Allow API endpoints, Swagger, and other system paths to pass through
        if (rootPath.Equals("api", StringComparison.OrdinalIgnoreCase) || 
            rootPath.Equals("index.html", StringComparison.OrdinalIgnoreCase) || 
            rootPath.StartsWith("swagger", StringComparison.OrdinalIgnoreCase) || 
            rootPath.Equals("images", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        try
        {
            if (!Ulid.TryParse(rootPath, out Ulid share))
            {
                await next(context);
                return;
            }

            if (!Providers.TryGetValue(share, out PhysicalFileProvider? provider))
            {
                await next(context);
                return;
            }

            string? relativePath = pathValue?[pathValue.IndexOf('/', 1)..];
            IFileInfo? file = relativePath != null ? provider.GetFileInfo(relativePath) : null;

            if (file?.PhysicalPath != null)
                await ServeFile(context, file);
            else
                await next(context);
        }
        catch
        {
            await next(context);
        }
    }

    private static async Task ServeFile(HttpContext context, IFileInfo file)
    {
        if (file.PhysicalPath is not { } filePhysicalPath) return;

        FileInfo fileInfo = new(filePhysicalPath);
        long fileLength = fileInfo.Length;

        context.Response.ContentType = MimeTypes.GetMimeTypeFromFile(file.PhysicalPath);

        bool isStreamableMedia = IsStreamableMedia(filePhysicalPath);
        bool hasRangeRequest = context.Request.Headers.TryGetValue("Range", out StringValues rangeValue);

        // Force partial content for streamable media files or when range is requested
        if (!hasRangeRequest && !isStreamableMedia)
        {
            await context.Response.SendFileAsync(file.PhysicalPath);
            return;
        }

        // Parse range or default to start of file for streamable media
        long start = 0;
        long end;

        if (hasRangeRequest)
        {
            string?[] ranges = rangeValue.ToString()
                .Replace("bytes=", "")
                .Split('-')
                .ToArray();

            start = Convert.ToInt64(ranges[0]);
            end = ranges.Length > 1 && !string.IsNullOrEmpty(ranges[1]) 
                ? Convert.ToInt64(ranges[1]) 
                : fileLength - 1;
        }
        else
        {
            // For streamable media without range request, serve initial chunk (e.g., first 1MB)
            end = Math.Min(start + (1024 * 1024) - 1, fileLength - 1);
        }

        long length = end - start + 1;

        context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
        context.Response.Headers.ContentRange = new ContentRangeHeaderValue(start, end, fileLength).ToString();
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentLength = length;

        await using FileStream fs = File.OpenRead(file.PhysicalPath);
        
        fs.Seek(start, SeekOrigin.Begin);
        byte[] buffer = new byte[64 * 1024];
        int bytesRead;
        long bytesToRead = length;

        while ((bytesRead = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesToRead))) > 0 && bytesToRead > 0)
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