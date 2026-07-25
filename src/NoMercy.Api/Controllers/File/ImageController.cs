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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MimeMapping;
using NoMercy.NmSystem.Images;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.Api.Controllers.File;

[Route("images/{type}/{path}")]
public class ImageController(
    IStorage storage,
    IImageService imageService,
    ILogger<ImageController> logger
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Image(
        string type,
        string path,
        [FromQuery] ImageConvertArguments request
    )
    {
        try
        {
            Response.Headers.Append("Expires", DateTime.UtcNow.AddDays(30).ToString("R"));
            Response.Headers.Append("Cache-Control", "public, max-age=2592000");
            Response.Headers.Append("Access-Control-Allow-Origin", "*");

            string folder = Path.Join(AppFiles.ImagesPath, ImageRequestPath.SanitizeSegment(type));
            if (!storage.Exists(folder))
                return NotFoundResponse("Image folder not found");

            string safeSegment = ImageRequestPath.SanitizeSegment(path);
            string filePath = Path.Join(folder, safeSegment);
            try
            {
                if (!storage.Exists(filePath) && type == "original")
                {
                    using Image<Rgba32>? downloadedImage = await TmdbImageClient.Download(
                        "/" + safeSegment
                    )!;
                }
            }
            catch (Exception)
            {
                //
            }

            if (!storage.Exists(filePath))
                return NotFoundResponse("Image not found");

            long originalFileSize = storage.Size(filePath);
            string originalMimeType = MimeUtility.GetMimeMapping(filePath);

            bool emptyArguments =
                request.Width is null && request.Type is null && request.Quality is null or 100;

            if (
                emptyArguments
                || path.Contains(".svg")
                || (
                    originalFileSize < request.Width
                    && originalMimeType == imageService.Parse(request.Type ?? "png").DefaultMimeType
                )
            )
                return PhysicalFile(filePath, originalMimeType);

            string encodedUrl = Request.GetEncodedUrl();

            string hashedUrl =
                CacheController.GenerateFileName(encodedUrl)
                + "."
                + imageService.Parse(request.Type ?? "png").FileExtensions.First();

            string cachedImagePath = Path.Join(AppFiles.TempImagesPath, hashedUrl);
            if (storage.Exists(cachedImagePath))
                return PhysicalFile(
                    cachedImagePath,
                    imageService.Parse(request.Type ?? "png").DefaultMimeType
                );

            try
            {
                (byte[] magickImage, string mimeType) = imageService.ResizeMagickNet(
                    filePath,
                    request.Width,
                    request.AspectRatio,
                    request.Type,
                    request.Quality
                );
                await storage.WriteAsync(cachedImagePath, magickImage, CancellationToken.None);

                return File(magickImage, mimeType);
            }
            catch (Exception e)
            {
                logger.LogWarning(
                    "Image conversion failed for {FilePath}: {Message}", [filePath, e.Message]
                );
                return PhysicalFile(filePath, originalMimeType);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return NotFoundResponse("Image not found");
        }
    }

    [HttpDelete]
    public IActionResult DeleteCache(
        string type,
        string path,
        [FromQuery] ImageConvertArguments request
    )
    {
        try
        {
            string encodedUrl = Request.GetEncodedUrl();

            string hashedUrl =
                CacheController.GenerateFileName(encodedUrl)
                + "."
                + imageService.Parse(request.Type ?? "png").FileExtensions.First();

            string cachedImagePath = Path.Join(AppFiles.TempImagesPath, hashedUrl);
            if (storage.Exists(cachedImagePath))
            {
                storage.Delete(cachedImagePath);
                return Ok(new { status = "ok", message = "Cache deleted" });
            }

            return NotFoundResponse("Cache not found");
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return InternalServerErrorResponse("Image cache operation failed");
        }
    }
}
