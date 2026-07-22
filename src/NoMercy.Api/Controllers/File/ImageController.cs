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

[Route(template: "images/{type}/{path}")]
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
            Response.Headers.Append(key: "Expires", value: DateTime.UtcNow.AddDays(value: 30).ToString(format: "R"));
            Response.Headers.Append(key: "Cache-Control", value: "public, max-age=2592000");
            Response.Headers.Append(key: "Access-Control-Allow-Origin", value: "*");

            string folder = Path.Join(path1: AppFiles.ImagesPath, path2: ImageRequestPath.SanitizeSegment(segment: type));
            if (!storage.Exists(path: folder))
                return NotFoundResponse(detail: "Image folder not found");

            string safeSegment = ImageRequestPath.SanitizeSegment(segment: path);
            string filePath = Path.Join(path1: folder, path2: safeSegment);
            try
            {
                if (!storage.Exists(path: filePath) && type == "original")
                {
                    using Image<Rgba32>? downloadedImage = await TmdbImageClient.Download(
                        path: "/" + safeSegment
                    )!;
                }
            }
            catch (Exception)
            {
                //
            }

            if (!storage.Exists(path: filePath))
                return NotFoundResponse(detail: "Image not found");

            long originalFileSize = storage.Size(path: filePath);
            string originalMimeType = MimeUtility.GetMimeMapping(file: filePath);

            bool emptyArguments =
                request.Width is null && request.Type is null && request.Quality is null or 100;

            if (
                emptyArguments
                || path.Contains(value: ".svg")
                || (
                    originalFileSize < request.Width
                    && originalMimeType == imageService.Parse(format: request.Type ?? "png").DefaultMimeType
                )
            )
                return PhysicalFile(physicalPath: filePath, contentType: originalMimeType);

            string encodedUrl = Request.GetEncodedUrl();

            string hashedUrl =
                CacheController.GenerateFileName(url: encodedUrl)
                + "."
                + imageService.Parse(format: request.Type ?? "png").FileExtensions.First();

            string cachedImagePath = Path.Join(path1: AppFiles.TempImagesPath, path2: hashedUrl);
            if (storage.Exists(path: cachedImagePath))
                return PhysicalFile(
                    physicalPath: cachedImagePath,
                    contentType: imageService.Parse(format: request.Type ?? "png").DefaultMimeType
                );

            try
            {
                (byte[] magickImage, string mimeType) = imageService.ResizeMagickNet(
                    image: filePath,
                    width: request.Width,
                    aspectRatio: request.AspectRatio,
                    type: request.Type,
                    quality: request.Quality
                );
                await storage.WriteAsync(path: cachedImagePath, bytes: magickImage, ct: CancellationToken.None);

                return File(fileContents: magickImage, contentType: mimeType);
            }
            catch (Exception e)
            {
                logger.LogWarning(
                    message: "Image conversion failed for {FilePath}: {Message}", args: [filePath, e.Message]
                );
                return PhysicalFile(physicalPath: filePath, contentType: originalMimeType);
            }
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
            return NotFoundResponse(detail: "Image not found");
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
                CacheController.GenerateFileName(url: encodedUrl)
                + "."
                + imageService.Parse(format: request.Type ?? "png").FileExtensions.First();

            string cachedImagePath = Path.Join(path1: AppFiles.TempImagesPath, path2: hashedUrl);
            if (storage.Exists(path: cachedImagePath))
            {
                storage.Delete(path: cachedImagePath);
                return Ok(value: new { status = "ok", message = "Cache deleted" });
            }

            return NotFoundResponse(detail: "Cache not found");
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
            return InternalServerErrorResponse(detail: "Image cache operation failed");
        }
    }
}
