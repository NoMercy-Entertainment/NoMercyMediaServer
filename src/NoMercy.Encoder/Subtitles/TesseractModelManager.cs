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

using System.Net;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Ensures Tesseract *.traineddata language models are available on disk,
/// downloading missing ones from the NoMercy-Entertainment/nomercy-tesseract
/// repository. Idempotent — repeat calls for an existing language are a no-op.
/// </summary>
public class TesseractModelManager(
    EncoderOptions options,
    HttpClient httpClient,
    IStorage storage,
    ILogger<TesseractModelManager> logger
) : ITesseractModelManager
{
    private const string RepositoryBaseUrl =
        "https://raw.githubusercontent.com/NoMercy-Entertainment/nomercy-tesseract/master/tessdata";

    public string ModelDirectory =>
        options.TesseractModelsDirectory
        ?? throw new InvalidOperationException(
            "TesseractModelsDirectory is not configured on EncoderOptions."
        );

    public async Task<string> EnsureLanguageModelAsync(string language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language code must be non-empty", nameof(language));

        storage.CreateDirectory(ModelDirectory);

        string fileName = $"{language}.traineddata";
        string localPath = Path.Combine(ModelDirectory, fileName);

        if (storage.Exists(localPath))
        {
            logger.LogDebug("Tesseract model already present: {FileName}", fileName);
            return localPath;
        }

        string url = $"{RepositoryBaseUrl}/{fileName}";
        logger.LogInformation("Downloading Tesseract model {FileName} from {Url}", fileName, url);

        using HttpResponseMessage response = await httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Tesseract model not found in repository: {fileName}"
            );
        }

        response.EnsureSuccessStatusCode();

        // Stream to a temp file then rename — never leave a half-written model on disk
        // if the download is cancelled midway.
        string tempPath = $"{localPath}.tmp";
        try
        {
            await using (Stream file = await storage.OpenWriteAsync(tempPath, overwrite: true, ct))
            {
                await using Stream remote = await response.Content.ReadAsStreamAsync(ct);
                await remote.CopyToAsync(file, ct);
            }

            // Atomic-ish swap: delete any prior model then move the
            // freshly downloaded temp into its final place.
            storage.Delete(localPath);
            storage.Move(tempPath, localPath);
        }
        catch
        {
            storage.Delete(tempPath);
            throw;
        }

        logger.LogInformation("Tesseract model saved: {Path}", localPath);
        return localPath;
    }

    public IReadOnlyList<string> GetAvailableLanguages()
    {
        // The repository list is not exposed — we only know what's in our configured
        // tessdata directory. Callers can probe EnsureLanguageModelAsync to pull more.
        return GetDownloadedLanguages();
    }

    public IReadOnlyList<string> GetDownloadedLanguages()
    {
        if (!storage.Exists(ModelDirectory))
            return [];

        return storage
            .List(ModelDirectory, "*.traineddata", recursive: false)
            .Where(e => !e.IsDirectory)
            .Select(e => Path.GetFileNameWithoutExtension(e.Path))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToArray();
    }
}
