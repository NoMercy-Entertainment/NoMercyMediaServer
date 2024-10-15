using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using Serilog.Events;

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
                    await TmdbImageClient.Download("/" + path);
                }
            }
            catch (Exception e)
            {
                //
            }

            if (!System.IO.File.Exists(filePath)) return NotFound();

            FileInfo fileInfo = new(filePath);
            long originalFileSize = fileInfo.Length;
            string? originalMimeType = MimeUtility.GetMimeMapping(filePath);

            bool emptyArguments = request.Width is null && request.Type is null && request.Quality is 100;

            if (emptyArguments || path.Contains(".svg") ||
                (originalFileSize < request.Width && originalMimeType == request.Format.DefaultMimeType))
                return PhysicalFile(filePath, originalMimeType);

            string hashedUrl = CacheController.GenerateFileName(Request.GetEncodedUrl()) + "." +
                               request.Format.FileExtensions.First();

            string cachedImagePath = Path.Join(AppFiles.TempImagesPath, hashedUrl);
            if (System.IO.File.Exists(cachedImagePath)) return PhysicalFile(cachedImagePath, request.Format.DefaultMimeType);

            (byte[] magickImage, string mimeType) = Images.ResizeMagickNet(filePath, request);
            await System.IO.File.WriteAllBytesAsync(cachedImagePath, magickImage);

            return File(magickImage,  mimeType);
        }
        catch (Exception e)
        {
            Logger.Error(e, LogEventLevel.Error);
            return NotFound();
        }
    }
}