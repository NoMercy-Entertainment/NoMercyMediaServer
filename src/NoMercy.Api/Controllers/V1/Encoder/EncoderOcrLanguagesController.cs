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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Encoder.Subtitles;
using NoMercy.Helpers.Extensions;
using NoMercy.Authorization;

namespace NoMercy.Api.Controllers.V1.Encoder;

/// <summary>
/// Endpoints for managing Tesseract language models — listing available /
/// downloaded models and triggering an on-demand download. Downloading a
/// model is owner-only; listing is open to any authenticated moderator.
/// </summary>
[ApiController]
[Tags("Encoder OCR Languages")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/encoder/ocr")]
public class EncoderOcrLanguagesController(ITesseractModelManager modelManager) : BaseController
{
    /// <summary>
    /// Returns all language codes that the Tesseract model registry knows
    /// about (<c>available</c>) and the subset that are already downloaded to
    /// disk (<c>downloaded</c>).
    /// </summary>
    [HttpGet("languages")]
    public IActionResult GetLanguages()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view OCR language models");

        return Ok(
            new
            {
                available = modelManager.GetAvailableLanguages(),
                downloaded = modelManager.GetDownloadedLanguages(),
            }
        );
    }

    /// <summary>
    /// Ensures a Tesseract language model is downloaded and ready. Returns
    /// the language code and the path to the <c>.traineddata</c> file.
    /// Owner-only — downloads can be large and slow.
    /// </summary>
    [HttpPost("languages/{code}/download")]
    public async Task<IActionResult> DownloadLanguage(string code, CancellationToken ct)
    {
        if (!User.IsOwner())
            return UnauthorizedResponse(
                "Only the server owner can download Tesseract language models"
            );

        if (string.IsNullOrWhiteSpace(code))
            return BadRequestResponse("Language code is required");

        try
        {
            string path = await modelManager.EnsureLanguageModelAsync(code, ct);
            return Ok(new { language = code, path });
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse(
                $"Failed to download language model '{code}': {ex.Message}"
            );
        }
    }
}
