using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace NoMercy.Api.Middleware;

public class DynamicStaticFilesMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<Ulid, PhysicalFileProvider> Providers = new();

    private static string GetContentType(string? extension) => extension switch
    {
        ".txt" => "text/plain",
        ".html" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        ".svg" => "image/svg+xml",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".mp4" => "video/mp4",
        ".mpeg" => "video/mpeg",
        ".vtt" => "text/vtt",
        ".srt" => "text/srt",
        ".webm" => "video/webm",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".eot" => "font/eot",
        _ => "application/octet-stream"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.HasValue)
        {
            await next(context);
            return;
        }

        string? pathValue = context.Request.Path.Value;
        string rootPath = context.Request.Path.ToString().Split('/')[1];
        if (rootPath == "api" || rootPath == "index.html" || rootPath.StartsWith("swagger") || rootPath == "images")
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
            {
                await ServeFile(context, file);
            }
            else
            {
                await next(context);
            }
        }
        catch
        {
            await next(context);
        }
    }

    private static async Task ServeFile(HttpContext context, IFileInfo file)
    {
        if (file.PhysicalPath is not { } filePhysicalPath) return;

        var fileInfo = new FileInfo(filePhysicalPath);
        long fileLength = fileInfo.Length;

        context.Response.ContentType = GetContentType(Path.GetExtension(file.PhysicalPath).ToLower());

        if (!context.Request.Headers.TryGetValue("Range", out StringValues rangeValue))
        {
            await context.Response.SendFileAsync(file.PhysicalPath);
            return;
        }

        string?[] ranges = rangeValue.ToString()
            .Replace("bytes=", "")
            .Split('-')
            .ToArray();

        long end = fileLength - 1;
        long start = Convert.ToInt64(ranges[0]);

        if (ranges.Length > 1 && !string.IsNullOrEmpty(ranges[1]))
        {
            end = Convert.ToInt64(ranges[1]);
        }

        long length = end - start + 1;

        context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
        context.Response.Headers.ContentRange = new ContentRangeHeaderValue(start, end, fileLength).ToString();
        context.Response.Headers.AcceptRanges = "bytes";
        context.Request.ContentLength = length;

        await using (FileStream fs = File.OpenRead(file.PhysicalPath))
        {
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
    }

    public static void AddPath(Ulid requestPath, string physicalPath)
    {
        Providers[requestPath] = new PhysicalFileProvider(physicalPath);
    }

    public static void RemovePath(Ulid requestPath)
    {
        Providers.TryRemove(requestPath, out _);
    }
}