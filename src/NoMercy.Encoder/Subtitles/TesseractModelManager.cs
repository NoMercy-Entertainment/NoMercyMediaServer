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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Ensures Tesseract *.traineddata language models are available on disk, downloading
/// missing ones through <see cref="ITesseractModelDownloader"/> — the signed
/// NoMercy-Entertainment/nomercy-tesseract release, never an unverified raw fetch.
/// Idempotent — repeat calls for an existing language are a no-op.
/// </summary>
public class TesseractModelManager(
    EncoderOptions options,
    ITesseractModelDownloader downloader,
    IStorage storage,
    ILogger<TesseractModelManager> logger
) : ITesseractModelManager
{
    public string ModelDirectory =>
        options.TesseractModelsDirectory
        ?? throw new InvalidOperationException(
            message: "TesseractModelsDirectory is not configured on EncoderOptions."
        );

    public async Task<string> EnsureLanguageModelAsync(string language, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value: language))
            throw new ArgumentException(message: "Language code must be non-empty", paramName: nameof(language));

        storage.CreateDirectory(path: ModelDirectory);

        string fileName = $"{language}.traineddata";
        string localPath = Path.Combine(path1: ModelDirectory, path2: fileName);

        if (storage.Exists(path: localPath))
        {
            logger.LogDebug(message: "Tesseract model already present: {FileName}", args: fileName);
            return localPath;
        }

        logger.LogInformation(
            message: "Downloading verified Tesseract model {FileName} from the signed nomercy-tesseract release",
            args: fileName
        );

        // Stream to a temp file then rename — never leave a half-written model on disk
        // if the download is cancelled midway. The downloader itself has already verified
        // the manifest signature and the SHA-256 before handing back any bytes, so nothing
        // reaches disk here unless it passed both checks.
        string tempPath = $"{localPath}.tmp";
        try
        {
            await using Stream verified = await downloader.DownloadVerifiedAsync(language: language, ct: ct);
            await using (Stream file = await storage.OpenWriteAsync(path: tempPath, overwrite: true, ct: ct))
            {
                await verified.CopyToAsync(destination: file, cancellationToken: ct);
            }

            // Atomic-ish swap: delete any prior model then move the
            // freshly downloaded temp into its final place.
            storage.Delete(path: localPath);
            storage.Move(from: tempPath, to: localPath);
        }
        catch
        {
            storage.Delete(path: tempPath);
            throw;
        }

        logger.LogInformation(message: "Tesseract model saved: {Path}", args: localPath);
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
        if (!storage.Exists(path: ModelDirectory))
            return [];

        return storage
            .List(path: ModelDirectory, pattern: "*.traineddata", recursive: false)
            .Where(predicate: e => !e.IsDirectory)
            .Select(selector: e => Path.GetFileNameWithoutExtension(path: e.Path))
            .Where(predicate: name => !string.IsNullOrEmpty(value: name))
            .Select(selector: name => name!)
            .ToArray();
    }
}
