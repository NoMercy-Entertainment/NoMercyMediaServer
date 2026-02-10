using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using NoMercy.Helpers;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.Api.Controllers.File;

[Route("images/{type}/{path}")]
public class ImageController : Controller
{
    [HttpGet]
    public async Task<IActionResult> Image(string type, string path, [FromQuery] ImageConvertArguments request)
    {
        try
        {
            Response.Headers.Append("Expires", DateTime.Now.AddDays(30) + " GMT");
            Response.Headers.Append("Cache-Control", "public, max-age=2592000");
            Response.Headers.Append("Access-Control-Allow-Origin", "*");

            string folder = Path.Join(AppFiles.ImagesPath, type);
            if (!Directory.Exists(folder)) return NotFound();

            string filePath = Path.Join(folder, path.Replace("/", ""));
            try
            {
                if (!System.IO.File.Exists(filePath) && type == "original")
                {
                    using Image<Rgba32>? downloadedImage = await TmdbImageClient.Download("/" + path)!;
                }
            }
            catch (Exception)
            {
                //
            }

            if (!System.IO.File.Exists(filePath)) return NotFound();

            FileInfo fileInfo = new(filePath);
            long originalFileSize = fileInfo.Length;
            string originalMimeType = MimeUtility.GetMimeMapping(filePath);

            bool emptyArguments = request.Width is null && request.Type is null && request.Quality is 100;

            if (emptyArguments || path.Contains(".svg") ||
                (originalFileSize < request.Width && originalMimeType == request.Format.DefaultMimeType))
                return PhysicalFile(filePath, originalMimeType);
            
            string encodedUrl = Request.GetEncodedUrl();

            string hashedUrl = CacheController.GenerateFileName(encodedUrl) + "." +
                               request.Format.FileExtensions.First();

            string cachedImagePath = Path.Join(AppFiles.TempImagesPath, hashedUrl);
            if (System.IO.File.Exists(cachedImagePath))
                return PhysicalFile(cachedImagePath, request.Format.DefaultMimeType);

            (byte[] magickImage, string mimeType) = Images.ResizeMagickNet(filePath, request);
            await System.IO.File.WriteAllBytesAsync(cachedImagePath, magickImage);

            return File(magickImage, mimeType);
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return NotFound();
        }
    }
    
    [HttpDelete]
    public IActionResult DeleteCache(string type, string path, [FromQuery] ImageConvertArguments request)
    {
        try
        {
            string encodedUrl = Request.GetEncodedUrl();

            string hashedUrl = CacheController.GenerateFileName(encodedUrl) + "." +
                               request.Format.FileExtensions.First();

            string cachedImagePath = Path.Join(AppFiles.TempImagesPath, hashedUrl);
            if (System.IO.File.Exists(cachedImagePath))
            {
                System.IO.File.Delete(cachedImagePath);
                return Ok(new { status = "ok", message = "Cache deleted" });
            }

            return NotFound(new { status = "error", message = "Cache not found" });
        }
        catch (Exception e)
        {
            Logger.App(e.Message, LogEventLevel.Error);
            return StatusCode(500, new { status = "error", message = "Internal server error" });
        }
    }
}